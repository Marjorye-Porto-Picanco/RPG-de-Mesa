// JavaScript for Monstros-lib component

<script>
    const gradeMonstros = document.querySelector("#grade-monstros");
    const modelosMonstros = Array.from(gradeMonstros.querySelectorAll(".monster-card"));
    const buscaMonstro = document.querySelector("#busca-monstro");
    const filtroTipo = document.querySelector("#filtro-tipo");
    const monstrosSalvos = JSON.parse(localStorage.getItem("salao-monstros") || "[]");

    monstrosSalvos.forEach((monstro) => {
        const cartao = document.createElement("article");
    cartao.className = "monster-card";
    cartao.dataset.nome = monstro.nome.toLocaleLowerCase("pt-BR");
    cartao.dataset.tipo = monstro.tipo;
    cartao.dataset.descricao = `${monstro.tipo} ${monstro.tamanho} ${monstro.tendencia} ${monstro.resistencias || ""} ${monstro.habilidades || ""}`.toLocaleLowerCase("pt-BR");
    const arte = document.createElement("div");
    arte.className = "monster-art";
    const imagem = document.createElement("img");
    imagem.src = monstro.imagem;
    imagem.alt = `Ilustração de ${monstro.nome}`;
    const desafio = document.createElement("span");
    desafio.className = "monster-cr";
    desafio.textContent = `ND ${monstro.nd}`;
    arte.append(imagem, desafio);
    const informacoes = document.createElement("div");
    informacoes.className = "monster-info";
    const nome = document.createElement("h2");
    nome.textContent = monstro.nome;
    const detalhes = document.createElement("p");
    detalhes.textContent = `${monstro.tamanho} · ${monstro.tendencia || "Sem alinhamento"}`;
    const etiquetas = document.createElement("div");
    etiquetas.className = "monster-tags";
    informacoes.append(nome, detalhes, etiquetas);
    cartao.append(arte, informacoes);
        (monstro.habilidades || "Criado por você").split(",").map((tag) => tag.trim()).filter(Boolean).slice(0, 3).forEach((tag) => {
            const etiqueta = document.createElement("span");
    etiqueta.textContent = tag;
    etiquetas.append(etiqueta);
        });
    gradeMonstros.prepend(cartao);
    });

    function filtrarMonstros() {
        const termo = buscaMonstro.value.trim().toLocaleLowerCase("pt-BR");
    const tipoSelecionado = filtroTipo.value;
    let visiveis = 0;
        gradeMonstros.querySelectorAll(".monster-card").forEach((cartao) => {
            const correspondeTexto = `${cartao.dataset.nome} ${cartao.dataset.descricao}`.includes(termo);
    const correspondeTipo = tipoSelecionado === "todos" || cartao.dataset.tipo === tipoSelecionado;
    cartao.hidden = !correspondeTexto || !correspondeTipo;
    if (!cartao.hidden) visiveis += 1;
        });
    let vazio = gradeMonstros.querySelector(".empty-state");
    if (visiveis === 0 && !vazio) {
        vazio = document.createElement("p");
    vazio.className = "empty-state";
    vazio.textContent = "Nenhuma criatura encontrada. Ajuste a busca ou cadastre um novo monstro.";
    gradeMonstros.append(vazio);
        } else if (visiveis > 0) {
        vazio?.remove();
        }
    }

    buscaMonstro.addEventListener("input", filtrarMonstros);
    filtroTipo.addEventListener("change", filtrarMonstros);
</script>