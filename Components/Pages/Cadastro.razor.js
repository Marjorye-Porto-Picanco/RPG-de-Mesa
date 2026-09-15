// JavaScript for Cadastro component
    function entrarAplicativo() {
        document.getElementById('tela-login').classList.remove('ativa');
    document.getElementById('tela-aplicativo').classList.add('ativa');
            }
    function mostrarCadastro() {
        document.getElementById('tela-login').classList.remove('ativa');
    document.getElementById('tela-cadastro').classList.add('ativa');
            }
    function mostrarLogin() {
        document.getElementById('tela-cadastro').classList.remove('ativa');
    document.getElementById('tela-login').classList.add('ativa');
            }
    function cadastrarConta(event) {
        event.preventDefault();
    alert('Conta criada com sucesso! Agora você já pode entrar na mesa.');
    mostrarLogin();
            }
    function sairAplicativo() {
        document.getElementById('tela-aplicativo').classList.remove('ativa');
    document.getElementById('tela-login').classList.add('ativa');
            }
            document.querySelectorAll('.aba[data-target]').forEach(aba => {
        aba.addEventListener('click', () => {
            document.querySelectorAll('.aba[data-target]').forEach(t => t.classList.remove('ativa'));
            aba.classList.add('ativa');
            document.querySelectorAll('.conteudo-aba').forEach(c => c.classList.remove('visivel'));
            document.getElementById(aba.dataset.target).classList.add('visivel');
        });
            });

    function rolarDoArduino() {
                const die = document.getElementById('face-dado');
    die.classList.add('rolando');
    let ticks = 0;
                const iv = setInterval(() => {
        die.textContent = Math.ceil(Math.random() * 20);
    die.style.transform = `rotate(${(ticks + 1) * 360}deg)`;
    ticks++;
                    if (ticks > 8) {
        clearInterval(iv);
    die.classList.remove('rolando');
    die.style.transform = '';
    const result = Math.ceil(Math.random() * 20);
    die.textContent = result;
    const registro = document.getElementById('registro');
    const entry = document.createElement('div');
    entry.className = 'entrada-registro arduino';
    entry.innerHTML = '<span class="autor">⚡ Dado físico</span> valor lido do Arduino <span class="rolagem">d20 = ' + result + '</span>';
    registro.appendChild(entry);
    registro.scrollTop = registro.scrollHeight;
                    }
                }, 70);
            }
