// JavaScript for monstro-criar component
<script>
    const formularioMonstro = document.querySelector("#form-monstro");
    const seletorImagem = formularioMonstro.elements.imagem;
    let imagemMonstro = "";
    seletorImagem.addEventListener("change", () => {
        imagemMonstro = "";
        document.querySelector("#imagem-previa").src = seletorImagem.value;
    });
    window.conectarUploadImagem(document.querySelector("#arquivo-monstro"), document.querySelector("#imagem-previa"), (imagem) => {
        imagemMonstro = imagem;
    });

    formularioMonstro.addEventListener("submit", (evento) => {
        evento.preventDefault();
        const dados = Object.fromEntries(new FormData(formularioMonstro).entries());
        dados.imagem = imagemMonstro || seletorImagem.value;
        const monstros = JSON.parse(localStorage.getItem("salao-monstros") || "[]");
        monstros.push({ ...dados, id: crypto.randomUUID(), criadoEm: new Date().toISOString() });
        localStorage.setItem("salao-monstros", JSON.stringify(monstros));
        document.querySelector("#status-monstro").textContent = "Monstro salvo. Abrindo a biblioteca...";
        window.location.href = "monstros-lib.html?novo=1";
    });
</script>