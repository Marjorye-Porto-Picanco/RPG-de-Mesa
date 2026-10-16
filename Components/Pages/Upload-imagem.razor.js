// JavaScript for Upload-imagem component
window.processarImagemSelecionada = (arquivo) => new Promise((resolver, rejeitar) => {
    if (!arquivo || !arquivo.type.startsWith("image/")) {
        rejeitar(new Error("Selecione um arquivo de imagem."));
        return;
    }

    if (arquivo.size > 12 * 1024 * 1024) {
        rejeitar(new Error("A imagem deve ter até 12 MB."));
        return;
    }

    const leitor = new FileReader();
    leitor.onerror = () => rejeitar(new Error("Não foi possível ler a imagem."));
    leitor.onload = () => {
        const imagem = new Image();
        imagem.onerror = () => rejeitar(new Error("Este arquivo de imagem não pôde ser aberto."));
        imagem.onload = () => {
            const limite = 1000;
            const escala = Math.min(1, limite / Math.max(imagem.naturalWidth, imagem.naturalHeight));
            const largura = Math.max(1, Math.round(imagem.naturalWidth * escala));
            const altura = Math.max(1, Math.round(imagem.naturalHeight * escala));
            const tela = document.createElement("canvas");
            tela.width = largura;
            tela.height = altura;
            const contexto = tela.getContext("2d");
            contexto.fillStyle = "#f3ead8";
            contexto.fillRect(0, 0, largura, altura);
            contexto.drawImage(imagem, 0, 0, largura, altura);
            resolver(tela.toDataURL("image/jpeg", 0.82));
        };
        imagem.src = leitor.result;
    };
    leitor.readAsDataURL(arquivo);
});

window.conectarUploadImagem = (entrada, previa, aoProcessar = () => { }) => {
    entrada.addEventListener("change", async () => {
        const arquivo = entrada.files?.[0];
        if (!arquivo) return;

        const controle = entrada.closest(".upload-imagem");
        const status = controle?.querySelector("[data-upload-status]");
        if (status) status.textContent = "Preparando imagem...";

        try {
            const imagem = await window.processarImagemSelecionada(arquivo);
            previa.src = imagem;
            previa.hidden = false;
            previa.classList.add("imagem-carregada");
            aoProcessar(imagem);
            if (status) status.textContent = "Imagem pronta.";
        } catch (erro) {
            entrada.value = "";
            if (status) status.textContent = erro.message;
        }
    });
};