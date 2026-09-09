// ============================================================================
// arduino_bridge.cpp
//
// Ponte entre o Arduino (dado físico) e a interface HTML/CSS/JS da mesa.
//
// O QUE ESTE PROGRAMA FAZ:
//   1. Abre a porta serial onde o Arduino está conectado (ex: /dev/ttyUSB0
//      no Linux, ou COM3 no Windows) e fica esperando linhas de texto do tipo
//      "ROLL:17" toda vez que o jogador gira o dado físico / aperta o botão.
//   2. Guarda o último valor lido.
//   3. Sobe um mini servidor HTTP local (http://localhost:8080/roll) que
//      devolve esse valor em JSON.
//
// O front-end (mesa.html) então troca a simulação por uma leitura real,
// bastando trocar a função rollFromArduino() para fazer um fetch() nesse
// endereço em vez de sortear um número aleatório (isso já está indicado
// no comentário no final do arquivo mesa.html/JS).
//
// COMPILAR (Linux/macOS):
//   g++ -std=c++17 -O2 arduino_bridge.cpp -o arduino_bridge -lpthread
//
// EXECUTAR:
//   ./arduino_bridge /dev/ttyUSB0 9600
//
// OBS: este exemplo usa termios (POSIX), ou seja, roda em Linux/macOS.
// No Windows a leitura da porta serial usa outra API (CreateFile /
// ReadFile do Windows.h) — a lógica de "ler linha, guardar valor,
// servir por HTTP" é a mesma, só a função read_serial_line() muda.
// ============================================================================

#include <atomic>
#include <cstring>
#include <fcntl.h>
#include <iostream>
#include <mutex>
#include <netinet/in.h>
#include <sstream>
#include <string>
#include <termios.h>
#include <thread>
#include <unistd.h>

// ---------------------------------------------------------------------------
// Estado compartilhado: o último valor de dado lido do Arduino.
// Protegido por mutex porque duas threads mexem nele: a que lê a serial
// e a que responde as requisições HTTP.
// ---------------------------------------------------------------------------
std::mutex g_mutex;
int g_ultimo_valor = 0;
long g_total_leituras = 0;

// ---------------------------------------------------------------------------
// Abre a porta serial (ex: "/dev/ttyUSB0") na velocidade informada (ex: 9600)
// e configura o modo "raw" (sem processamento de linha pelo próprio SO).
// ---------------------------------------------------------------------------
int abrir_porta_serial(const std::string& caminho, int velocidade) {
    int fd = open(caminho.c_str(), O_RDWR | O_NOCTTY | O_SYNC);
    if (fd < 0) {
        std::cerr << "Erro ao abrir " << caminho << ": " << strerror(errno) << "\n";
        return -1;
    }

    termios tty{};
    if (tcgetattr(fd, &tty) != 0) {
        std::cerr << "Erro em tcgetattr: " << strerror(errno) << "\n";
        return -1;
    }

    speed_t baud = (velocidade == 115200) ? B115200 : B9600;
    cfsetospeed(&tty, baud);
    cfsetispeed(&tty, baud);

    tty.c_cflag = (tty.c_cflag & ~CSIZE) | CS8;   // 8 bits de dado
    tty.c_iflag &= ~IGNBRK;
    tty.c_lflag = 0;                              // modo não-canônico (raw)
    tty.c_oflag = 0;
    tty.c_cc[VMIN] = 0;
    tty.c_cc[VTIME] = 5;                          // timeout de leitura (0.5s)
    tty.c_iflag &= ~(IXON | IXOFF | IXANY);
    tty.c_cflag |= (CLOCAL | CREAD);
    tty.c_cflag &= ~(PARENB | PARODD);            // sem paridade
    tty.c_cflag &= ~CSTOPB;                       // 1 stop bit
    tty.c_cflag &= ~CRTSCTS;                      // sem controle de fluxo

    if (tcsetattr(fd, TCSANOW, &tty) != 0) {
        std::cerr << "Erro em tcsetattr: " << strerror(errno) << "\n";
        return -1;
    }
    return fd;
}

// ---------------------------------------------------------------------------
// Thread que fica lendo a serial linha por linha, esperando algo como
// "ROLL:14". Quando encontra, atualiza g_ultimo_valor.
// ---------------------------------------------------------------------------
void thread_leitura_serial(int fd) {
    std::string buffer;
    char c;
    while (true) {
        int n = read(fd, &c, 1);
        if (n <= 0) continue;

        if (c == '\n') {
            // Espera-se o formato: ROLL:<numero>
            auto pos = buffer.find("ROLL:");
            if (pos != std::string::npos) {
                try {
                    int valor = std::stoi(buffer.substr(pos + 5));
                    std::lock_guard<std::mutex> lock(g_mutex);
                    g_ultimo_valor = valor;
                    g_total_leituras++;
                    std::cout << "[Arduino] Dado lido: " << valor << "\n";
                } catch (...) {
                    // linha corrompida/ruído na serial: ignora
                }
            }
            buffer.clear();
        } else if (c != '\r') {
            buffer += c;
        }
    }
}

// ---------------------------------------------------------------------------
// Servidor HTTP minimalista: responde qualquer requisição em /roll com
// um JSON { "valor": N, "leituras": M }. É só o suficiente para o
// fetch() do JavaScript da página consumir — não é um servidor de
// produção, é o bastante para o projeto acadêmico.
// ---------------------------------------------------------------------------
void thread_servidor_http(int porta) {
    int server_fd = socket(AF_INET, SOCK_STREAM, 0);
    int opt = 1;
    setsockopt(server_fd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = INADDR_ANY;
    addr.sin_port = htons(porta);

    bind(server_fd, (sockaddr*)&addr, sizeof(addr));
    listen(server_fd, 10);
    std::cout << "[HTTP] Servindo leituras em http://localhost:" << porta << "/roll\n";

    while (true) {
        int client = accept(server_fd, nullptr, nullptr);
        if (client < 0) continue;

        char req[1024];
        read(client, req, sizeof(req) - 1);

        int valor, leituras;
        {
            std::lock_guard<std::mutex> lock(g_mutex);
            valor = g_ultimo_valor;
            leituras = g_total_leituras;
        }

        std::ostringstream corpo;
        corpo << "{\"valor\":" << valor << ",\"leituras\":" << leituras << "}";

        std::ostringstream resposta;
        resposta << "HTTP/1.1 200 OK\r\n"
                  << "Content-Type: application/json\r\n"
                  << "Access-Control-Allow-Origin: *\r\n"   // permite o fetch() da página
                  << "Content-Length: " << corpo.str().size() << "\r\n"
                  << "Connection: close\r\n\r\n"
                  << corpo.str();

        std::string resp_str = resposta.str();
        write(client, resp_str.c_str(), resp_str.size());
        close(client);
    }
}

int main(int argc, char** argv) {
    std::string porta_serial = (argc > 1) ? argv[1] : "/dev/ttyUSB0";
    int velocidade = (argc > 2) ? std::stoi(argv[2]) : 9600;

    int fd = abrir_porta_serial(porta_serial, velocidade);
    if (fd < 0) {
        std::cerr << "Não foi possível abrir a porta serial. Encerrando.\n";
        return 1;
    }

    std::thread t_serial(thread_leitura_serial, fd);
    std::thread t_http(thread_servidor_http, 8080);

    t_serial.join();
    t_http.join();
    return 0;
}

/* ============================================================================
 * ESBOÇO DO SKETCH NO ARDUINO (arduino_dado.ino):
 * ----------------------------------------------------------------------------
 * const int PINO_BOTAO = 2;
 *
 * void setup() {
 *   Serial.begin(9600);
 *   pinMode(PINO_BOTAO, INPUT_PULLUP);
 *   randomSeed(analogRead(A0));   // semente aleatória
 * }
 *
 * void loop() {
 *   if (digitalRead(PINO_BOTAO) == LOW) {     // jogador apertou o botão físico
 *     int valor = random(1, 21);              // simula um d20 (troque para o dado desejado)
 *     Serial.print("ROLL:");
 *     Serial.println(valor);
 *     delay(400);                             // evita leituras repetidas (debounce)
 *   }
 * }
 * ----------------------------------------------------------------------------
 *
 * TROCA NO FRONT-END (dentro de mesa.html, função rollFromArduino):
 * ----------------------------------------------------------------------------
 * async function rollFromArduino(){
 *   const resp = await fetch('http://localhost:8080/roll');
 *   const dados = await resp.json();
 *   // ... usar dados.valor no lugar do Math.random() ...
 * }
 * ============================================================================
 */
