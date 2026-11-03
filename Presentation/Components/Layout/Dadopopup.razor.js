// JavaScript for Dadopopup component
<script>
	const parametrosDado = new URLSearchParams(window.location.search);
	const campoQuantidade = document.querySelector("#quantidade-dados");
	const campoModificador = document.querySelector("#modificador-dado");
	const campoContexto = document.querySelector("#contexto-dado");
	const facesPadrao = [4, 6, 8, 10, 12, 20, 100];
	let facesDado = Number(parametrosDado.get("faces")) || 20;
	campoModificador.value = Number(parametrosDado.get("modifier")) || 0;
	campoContexto.textContent = parametrosDado.get("label") || "Rolagem da mesa";

	function selecionarDado(faces) {
		facesDado = facesPadrao.includes(faces) ? faces : 20;
		document.querySelectorAll(".dice-face").forEach((botao) => {
			const selecionado = Number(botao.dataset.faces) === facesDado;
	botao.classList.toggle("selecionado", selecionado);
	botao.setAttribute("aria-pressed", String(selecionado));
		});
	}

	document.querySelectorAll(".dice-face").forEach((botao) => {
		botao.addEventListener("click", () => selecionarDado(Number(botao.dataset.faces)));
	});
	selecionarDado(facesDado);

	document.querySelector("#form-rolagem").addEventListener("submit", (evento) => {
		evento.preventDefault();
	const quantidade = Math.min(20, Math.max(1, Number(campoQuantidade.value) || 1));
	const modificador = Math.min(100, Math.max(-100, Number(campoModificador.value) || 0));
	campoQuantidade.value = quantidade;
	campoModificador.value = modificador;
	const resultados = Array.from({length: quantidade }, () => Math.floor(Math.random() * facesDado) + 1);
		const subtotal = resultados.reduce((soma, resultado) => soma + resultado, 0);
	const total = subtotal + modificador;
	const expressao = `${quantidade}d${facesDado}${modificador > 0 ? ` + ${modificador}` : modificador < 0 ? ` - ${Math.abs(modificador)}` : ""}`;
	document.querySelector("#total-dado").textContent = total;
	document.querySelector("#expressao-dado").textContent = `${expressao} · ${resultados.join(" + ")}${modificador ? ` ${modificador > 0 ? "+" : "−"} ${Math.abs(modificador)}` : ""}`;
	const detalhe = document.querySelector("#detalhe-dados");
		detalhe.replaceChildren(...resultados.map((resultado) => {
			const peca = document.createElement("span");
	peca.textContent = resultado;
	return peca;
		}));
	if (modificador) {
			const pecaModificador = document.createElement("span");
	pecaModificador.className = "modifier-result";
	pecaModificador.textContent = `${modificador > 0 ? "+" : "−"}${Math.abs(modificador)}`;
	detalhe.append(pecaModificador);
		}
	document.querySelector("#resultado-dado").hidden = false;
	window.parent.postMessage({type: "dice-result", label: campoContexto.textContent, expression: expressao, total, rolls: resultados }, "*");
	});

	document.querySelector("#fechar-popup").addEventListener("click", () => {
		window.parent.postMessage({ type: "dice-close" }, "*");
	});
</script>
