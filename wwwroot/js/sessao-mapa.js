export function ehTelaMobile() {
    return window.matchMedia("(max-width: 560px)").matches;
}

export function inicializarArrasteMapa(gradeEl, dotNetRef) {
    if (!gradeEl || !dotNetRef) {
        return { dispose() { } };
    }

    const LIMIAR_ARRASTE_PX = 6;
    let ativo = null;

    function encontrarCelula(clientX, clientY) {
        const elemento = document.elementFromPoint(clientX, clientY);
        if (!elemento) return null;

        const celula = elemento.closest(".celula-grade");
        return celula && gradeEl.contains(celula) ? celula : null;
    }

    function atualizarCelulaAlvo(celula) {
        if (!ativo || ativo.celulaAlvo === celula) return;

        ativo.celulaAlvo?.classList.remove("celula-drop-alvo");
        ativo.celulaAlvo = celula;
        ativo.celulaAlvo?.classList.add("celula-drop-alvo");
    }

    function limparEstado() {
        if (!ativo) return;

        const estado = ativo;
        ativo = null;

        estado.celulaAlvo?.classList.remove("celula-drop-alvo");
        estado.pinoEl?.classList.remove("arrastando");
        estado.salaEl?.classList.remove("em-arraste-pino");

        try {
            if (estado.pinoEl?.hasPointerCapture?.(estado.pointerId)) {
                estado.pinoEl.releasePointerCapture(estado.pointerId);
            }
        } catch {
            // Ignora caso o ponteiro já tenha sido liberado pelo navegador
        }

        window.removeEventListener("pointermove", aoMover);
        window.removeEventListener("pointerup", aoSoltar, true);
        window.removeEventListener("pointercancel", aoCancelar, true);
        window.removeEventListener("blur", aoCancelar);
    }

    function cancelarArraste() {
        limparEstado();
    }

    function aoPressionar(e) {
        if (!e.isPrimary) {
            if (ativo) cancelarArraste();
            return;
        }

        if (e.pointerType === "mouse" && e.button !== 0) {
            return;
        }

        if (ativo) {
            cancelarArraste();
        }

        const mapArt = gradeEl.closest(".map-art");
        if (mapArt && (mapArt.classList.contains("modo-posicionar") || mapArt.classList.contains("modo-disparada"))) {
            return;
        }

        const pinoEl = e.target?.closest?.(".mapa-pino.arrastavel");
        if (!pinoEl || !gradeEl.contains(pinoEl)) {
            return;
        }

        const pinoId = Number.parseInt(pinoEl.dataset.pinoId ?? "", 10);
        const celulaOrigem = pinoEl.closest(".celula-grade");
        const originCol = Number.parseInt(celulaOrigem?.dataset.col ?? "", 10);
        const originRow = Number.parseInt(celulaOrigem?.dataset.row ?? "", 10);

        if (!Number.isFinite(pinoId) || !Number.isFinite(originCol) || !Number.isFinite(originRow)) {
            return;
        }

        ativo = {
            pointerId: e.pointerId,
            pinoId,
            pinoEl,
            originCol,
            originRow,
            startX: e.clientX,
            startY: e.clientY,
            arrastando: false,
            saiuDaOrigem: false,
            celulaAlvo: null,
            salaEl: gradeEl.closest(".sala-jogo")
        };

        window.addEventListener("pointermove", aoMover, { passive: false });
        window.addEventListener("pointerup", aoSoltar, true);
        window.addEventListener("pointercancel", aoCancelar, true);
        window.addEventListener("blur", aoCancelar);
    }

    function aoMover(e) {
        if (!ativo || e.pointerId !== ativo.pointerId) return;

        const dx = e.clientX - ativo.startX;
        const dy = e.clientY - ativo.startY;
        const celula = encontrarCelula(e.clientX, e.clientY);
        const col = celula ? Number.parseInt(celula.dataset.col ?? "", 10) : NaN;
        const row = celula ? Number.parseInt(celula.dataset.row ?? "", 10) : NaN;
        const outraCelula = Number.isFinite(col) && Number.isFinite(row)
            && (col !== ativo.originCol || row !== ativo.originRow);

        if (!ativo.arrastando && (Math.hypot(dx, dy) >= LIMIAR_ARRASTE_PX || outraCelula)) {
            ativo.arrastando = true;
            ativo.pinoEl.classList.add("arrastando");
            ativo.salaEl?.classList.add("em-arraste-pino");

            try {
                ativo.pinoEl.setPointerCapture?.(e.pointerId);
            } catch {
                // Em touch a captura implícita já mantém os eventos no alvo
            }
        }

        if (!ativo.arrastando) return;

        if (e.cancelable) {
            e.preventDefault();
        }

        if (outraCelula || !celula) {
            ativo.saiuDaOrigem = true;
        }

        atualizarCelulaAlvo(celula);
    }

    function aoSoltar(e) {
        if (!ativo || e.pointerId !== ativo.pointerId) return;

        const estado = ativo;
        const celula = encontrarCelula(e.clientX, e.clientY);
        limparEstado();

        const col = celula ? Number.parseInt(celula.dataset.col ?? "", 10) : NaN;
        const row = celula ? Number.parseInt(celula.dataset.row ?? "", 10) : NaN;
        const celulaValida = Number.isFinite(col) && Number.isFinite(row);

        // Toque curto sem iniciar arraste: seleciona o pino
        if (!estado.arrastando) {
            if (celulaValida && col === estado.originCol && row === estado.originRow) {
                dotNetRef.invokeMethodAsync("AoToqueCurtoPinoJsAsync", estado.pinoId).catch(() => { });
            }
            return;
        }

        // Soltou fora do mapa: encerra sem mover
        if (!celulaValida) {
            return;
        }

        // Soltou na casa original
        if (col === estado.originCol && row === estado.originRow) {
            if (!estado.saiuDaOrigem) {
                dotNetRef.invokeMethodAsync("AoToqueCurtoPinoJsAsync", estado.pinoId).catch(() => { });
            }
            return;
        }

        // Soltou em casa válida diferente: move o pino pela identidade exata
        dotNetRef.invokeMethodAsync("AoSoltarPinoJsAsync", estado.pinoId, col, row).catch(() => { });
    }

    function aoCancelar(e) {
        if (!ativo) return;
        if (e?.pointerId !== undefined && e.pointerId !== ativo.pointerId) return;
        cancelarArraste();
    }

    gradeEl.addEventListener("pointerdown", aoPressionar);

    return {
        dispose() {
            cancelarArraste();
            gradeEl.removeEventListener("pointerdown", aoPressionar);
        }
    };
}