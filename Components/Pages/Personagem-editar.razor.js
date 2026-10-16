// JavaScript for Personagem-editar component
<script>
    const personagemId = new URLSearchParams(window.location.search).get("id");
    const personagemSalvo = JSON.parse(localStorage.getItem("salao-personagens") || "[]").find((personagem) => personagem.id === personagemId);
    const imagemPersonagemAtual = document.querySelector("#imagem-personagem-atual");

    if (personagemSalvo) {
        if (personagemSalvo.imagem) imagemPersonagemAtual.src = personagemSalvo.imagem;
    document.querySelector("#nome-personagem").textContent = personagemSalvo.nome;
    document.querySelector(".descricao-personagem").textContent = `${personagemSalvo.raca} · ${personagemSalvo.classe} · Nível ${personagemSalvo.nivel}`;
    document.querySelector(".marcador-classe b").textContent = personagemSalvo.classe;
    const dados = document.querySelector(".dados-personagem");
    dados.replaceChildren();
        [["Raça", personagemSalvo.raca], ["Tendência", personagemSalvo.tendencia], ["Antecedente", personagemSalvo.antecedente || "Não informado"]].forEach(([rotulo, valor]) => {
            const item = document.createElement("span");
    const titulo = document.createElement("b");
    titulo.textContent = rotulo;
    item.append(titulo, ` ${valor}`);
    dados.append(item);
        });
    const atributosSalvos = ["forca", "destreza", "constituicao", "inteligencia", "sabedoria", "carisma"];
        document.querySelectorAll(".atributo").forEach((atributo, indice) => {
            const valor = Number(personagemSalvo[atributosSalvos[indice]] || 10);
    atributo.querySelector("b").textContent = valor;
    const modificador = Math.floor((valor - 10) / 2);
            atributo.querySelector("small").textContent = modificador > 0 ? `+${modificador}` : String(modificador);
        });
    const historia = document.querySelector(".historia p");
    historia.textContent = personagemSalvo.historia || "Ainda sem história registrada.";
    } else {
        const imagemLyra = localStorage.getItem("salao-personagem-imagem-lyra");
    if (imagemLyra) imagemPersonagemAtual.src = imagemLyra;
    }

    window.conectarUploadImagem(document.querySelector("#imagem-personagem-edicao"), imagemPersonagemAtual, (imagem) => {
        if (personagemSalvo) {
        personagemSalvo.imagem = imagem;
    const personagens = JSON.parse(localStorage.getItem("salao-personagens") || "[]");
            const indice = personagens.findIndex((personagem) => personagem.id === personagemId);
            if (indice >= 0) personagens[indice] = personagemSalvo;
    localStorage.setItem("salao-personagens", JSON.stringify(personagens));
        } else {
        localStorage.setItem("salao-personagem-imagem-lyra", imagem);
        }
    });

    const abasFicha = document.querySelectorAll(".aba-ficha");
    const visoesFicha = document.querySelectorAll(".visao-ficha");

    abasFicha.forEach((aba) => {
        aba.addEventListener("click", () => {
            abasFicha.forEach((item) => {
                const selecionada = item === aba;
                item.classList.toggle("ativa", selecionada);
                item.setAttribute("aria-selected", String(selecionada));
            });

            visoesFicha.forEach((visao) => {
                visao.hidden = visao.id !== aba.dataset.visao;
            });
        });
    });

    const parametrosFicha = new URLSearchParams(window.location.search);
    const abaInicial = parametrosFicha.get("aba");
    if (abaInicial) document.querySelector(`[data-visao="visao-${abaInicial}"]`)?.click();

    const formularioInventario = document.querySelector("#form-inventario");
    const modeloItem = document.querySelector("#modelo-item");
    const chaveInventario = "salao-da-mesa-inventario-lyra";
    const capacidadeCarga = 76.3;

    function atualizarCargaInventario() {
        const peso = Array.from(formularioInventario.querySelectorAll("[data-lista] .item-linha"))
            .reduce((total, linha) => {
                const quantidade = Number(linha.querySelector('[data-campo="quantidade"]').value) || 0;
    const pesoUnitario = Number(linha.querySelector('[data-campo="peso"]').value) || 0;
    return total + quantidade * pesoUnitario;
            }, 0);
    document.querySelector("#peso-atual-texto").textContent = `${peso.toFixed(1).replace(".", ",")} kg`;
    document.querySelector("#barra-carga").value = Math.min(peso, capacidadeCarga);
    }

    function criarLinhaInventario(dados = { }) {
        const linha = modeloItem.content.firstElementChild.cloneNode(true);
    linha.querySelector('[data-campo="nome"]').value = dados.nome || "";
    linha.querySelector('[data-campo="quantidade"]').value = dados.quantidade ?? 1;
    linha.querySelector('[data-campo="peso"]').value = dados.peso ?? 0;
    linha.querySelector('[data-campo="valor"]').value = dados.valor || "";
    return linha;
    }

    function salvarInventario() {
        const itens = { };
        formularioInventario.querySelectorAll("[data-lista]").forEach((lista) => {
        itens[lista.dataset.lista] = Array.from(lista.querySelectorAll(".item-linha"), (linha) => ({
            nome: linha.querySelector('[data-campo="nome"]').value,
            quantidade: linha.querySelector('[data-campo="quantidade"]').value,
            peso: linha.querySelector('[data-campo="peso"]').value,
            valor: linha.querySelector('[data-campo="valor"]').value
        }));
        });
        const moedas = Object.fromEntries(Array.from(formularioInventario.querySelectorAll(".moeda input"), (campo) => [campo.name, campo.value]));
    localStorage.setItem(chaveInventario, JSON.stringify({itens, moedas, notas: formularioInventario.elements.notas.value }));
    document.querySelector("#status-inventario").textContent = "Inventário salvo neste navegador.";
    }

    function carregarInventario() {
        const salvo = localStorage.getItem(chaveInventario);
    if (!salvo) return;
    try {
            const dados = JSON.parse(salvo);
    Object.entries(dados.itens || { }).forEach(([categoria, itens]) => {
                const lista = formularioInventario.querySelector(`[data-lista="${categoria}"]`);
                if (lista) lista.replaceChildren(...itens.map((item) => criarLinhaInventario(item)));
            });
    Object.entries(dados.moedas || { }).forEach(([nome, valor]) => {
                const campo = formularioInventario.elements.namedItem(nome);
    if (campo) campo.value = valor;
            });
    if (typeof dados.notas === "string") formularioInventario.elements.notas.value = dados.notas;
        } catch {
        localStorage.removeItem(chaveInventario);
        }
    }

    formularioInventario.addEventListener("click", (evento) => {
        const adicionar = evento.target.closest("[data-adicionar]");
    if (adicionar) {
            const lista = document.getElementById(adicionar.dataset.adicionar);
    const linha = criarLinhaInventario();
    lista.append(linha);
    linha.querySelector('[data-campo="nome"]').focus();
    atualizarCargaInventario();
    return;
        }
    const remover = evento.target.closest(".remover-item");
    if (remover) {
        remover.closest(".item-linha").remove();
    atualizarCargaInventario();
        }
    });
    formularioInventario.addEventListener("input", atualizarCargaInventario);
    formularioInventario.addEventListener("submit", (evento) => {
        evento.preventDefault();
    salvarInventario();
    });
    carregarInventario();
    atualizarCargaInventario();
</script>