using System.Text;

namespace RPGdeMesa.Presentation.Services;

internal sealed record RelatorioFalha(string NomeArquivo, string Texto, bool SalvoLocalmente);

internal static class FalhasApp
{
    private const string ArquivoPendente = "erro-pendente.txt";
    private static readonly object Trava = new();
    private static RelatorioFalha? _relatorio;
    private static int _inicializado;

    internal static void Inicializar()
    {
        if (Interlocked.Exchange(ref _inicializado, 1) != 0)
            return;

        AppDomain.CurrentDomain.UnhandledException += (_, evento) =>
        {
            if (evento.ExceptionObject is Exception erro)
                Registrar(erro, ".NET");
        };
        TaskScheduler.UnobservedTaskException += (_, evento) =>
        {
            evento.SetObserved();
            Tratar(evento.Exception, "Tarefa em segundo plano");
        };
    }

    internal static bool Tratar(Exception erro, string origem)
    {
        var relatorio = Registrar(erro, origem);
        try
        {
            return Microsoft.Maui.Controls.Application.Current is App app && app.MostrarFalha(relatorio);
        }
        catch
        {
            // Se a janela não puder renderizar, o relatório fica pendente para a próxima abertura.
            return false;
        }
    }

    internal static RelatorioFalha Registrar(Exception erro, string origem)
    {
        lock (Trava)
        {
            if (_relatorio is not null)
                return _relatorio;

            var agora = DateTimeOffset.Now;
            string texto = $"RPG de Mesa — relatório de erro{Environment.NewLine}" +
                $"Data: {agora:O}{Environment.NewLine}" +
                $"Origem: {origem}{Environment.NewLine}" +
                $"Sistema: {Environment.OSVersion}{Environment.NewLine}" +
                $".NET: {Environment.Version}{Environment.NewLine}{Environment.NewLine}{erro}";

            bool salvo = false;
            try
            {
                string pendente = Path.Combine(FileSystem.AppDataDirectory, ArquivoPendente);
                string temporario = pendente + ".tmp";
                File.WriteAllText(temporario, texto, Encoding.UTF8);
                File.Move(temporario, pendente, overwrite: true);
                salvo = true;
            }
            catch
            {
                // O seletor de arquivos também consegue exportar o texto mantido em memória.
            }

            return _relatorio = new(NomeRelatorio(agora), texto, salvo);
        }
    }

    internal static RelatorioFalha? LerPendente()
    {
        lock (Trava)
        {
            if (_relatorio is not null)
                return _relatorio;

            try
            {
                string arquivo = Path.Combine(FileSystem.AppDataDirectory, ArquivoPendente);
                if (File.Exists(arquivo))
                    _relatorio = new(NomeRelatorio(File.GetLastWriteTime(arquivo)), File.ReadAllText(arquivo), true);
            }
            catch
            {
                // Um arquivo indisponível não impede a abertura do aplicativo.
            }

            return _relatorio;
        }
    }

    internal static void Concluir(RelatorioFalha relatorio)
    {
        lock (Trava)
        {
            if (!relatorio.SalvoLocalmente)
                return;

            try
            {
                string pasta = FileSystem.AppDataDirectory;
                File.Move(Path.Combine(pasta, ArquivoPendente), Path.Combine(pasta, "ultimo-erro.txt"), overwrite: true);
            }
            catch
            {
                // Mantém o relatório pendente caso o armazenamento esteja inacessível.
            }
        }
    }

    private static string NomeRelatorio(DateTimeOffset data) => $"erro-rpg-{data:yyyy-MM-dd_HH-mm-ss}.txt";
}
