// JavaScript for Personagem-criar component
<script>
    const formularioPersonagem = document.querySelector("#form-personagem");
    let imagemPersonagem = "";
    window.conectarUploadImagem(document.querySelector("#imagem-personagem"), document.querySelector("#imagem-personagem-preview"), (imagem) => {
        imagemPersonagem = imagem;
    });

    formularioPersonagem.addEventListener("submit", (evento) => {
        evento.preventDefault();
    const valores = Object.fromEntries(new FormData(formularioPersonagem).entries());
    valores.imagem = imagemPersonagem;
    const personagens = JSON.parse(localStorage.getItem("salao-personagens") || "[]");
    personagens.push({...valores, id: crypto.randomUUID(), criadoEm: new Date().toISOString() });
    localStorage.setItem("salao-personagens", JSON.stringify(personagens));
    document.querySelector("#status-personagem").textContent = "Personagem criado. Abrindo sua biblioteca...";
    window.location.href = "personagens-lib.html?novo=1";
    });
</script>