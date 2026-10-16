// JavaScript for Home component
<script>
    const nomePerfilSalvo = JSON.parse(localStorage.getItem("salao-perfil") || "{}").nome;
    const nomeExibido = nomePerfilSalvo || "Mestre da mesa";
    const saudacao = document.querySelector("[data-saudar]");
    saudacao.textContent = nomeExibido.split(" ")[0];
    document.querySelector("#data-home").textContent = new Intl.DateTimeFormat("pt-BR", { dateStyle: "full" }).format(new Date());
</script>