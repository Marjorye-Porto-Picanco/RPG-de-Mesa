# RPG de Mesa

Aplicativo multiplataforma para apoiar a organização e a execução de campanhas de RPG de mesa. O projeto reúne gerenciamento de campanhas, personagens e conteúdo de jogo em uma interface construída com .NET MAUI e Blazor Hybrid.

> **Estado da documentação:** este documento descreve o código e a configuração encontrados no repositório. A solução contém referências a Android, iOS, Mac Catalyst e Windows. A integração com Arduino é citada no README anterior do projeto, mas não foi localizada no código-fonte examinado; por isso, não é descrita como funcionalidade implementada.

## Sumário

- [Visão geral](#visão-geral)
- [Funcionalidades](#funcionalidades)
- [Tecnologias](#tecnologias)
- [Arquitetura](#arquitetura)
- [Estrutura do repositório](#estrutura-do-repositório)
- [Telas e rotas](#telas-e-rotas)
- [Modelo de dados](#modelo-de-dados)
- [Configuração e execução](#configuração-e-execução)
- [Persistência e configuração do banco](#persistência-e-configuração-do-banco)
- [Fluxos principais](#fluxos-principais)
- [Limitações conhecidas e próximos passos](#limitações-conhecidas-e-próximos-passos)

## Visão geral

RPG de Mesa é um aplicativo cliente nativo que hospeda uma interface Blazor dentro do `BlazorWebView` do .NET MAUI. A interface é composta por páginas Razor e componentes reutilizáveis. Os ViewModels coordenam as páginas, os serviços de aplicação executam os casos de uso, os repositórios implementam acesso a dados e o `AppDbContext` mapeia o modelo para o SQL Server.

O domínio cobre contas e perfis, campanhas, fichas de personagens, bestiário, NPCs, itens, magias e sessões de mesa. Os recursos de sessão persistidos incluem participantes, mensagens, rolagens representadas no histórico e pinos posicionados no mapa.

## Funcionalidades

- Cadastro, autenticação e atualização de perfil de usuário.
- Criação, consulta, atualização e exclusão de campanhas; participação como mestre ou jogador por código de convite.
- Envio de fichas para avaliação do mestre, aprovação, recusa com motivo, reenvio e remoção da campanha.
- Criação e edição de personagens, com atributos, características, vínculo de campanha e inventário.
- Biblioteca de monstros, NPCs, itens, magias e mapas usados pelo usuário ou por campanhas.
- Criação, abertura, entrada, sincronização e encerramento de sessões de campanha.
- Gerenciamento do mapa ativo e posicionamento, visibilidade e remoção de pinos.
- Chat da sessão, histórico de mensagens, registro de rolagens e revisão pós-sessão de ficha/resumo/XP.

As funcionalidades são identificadas a partir das páginas, ViewModels, serviços e métodos disponíveis. A disponibilidade de cada fluxo pode depender da configuração correta do banco e do estado da conta/campanha.

## Tecnologias

| Área | Tecnologia encontrada |
|---|---|
| Aplicativo | .NET MAUI, projeto único (`UseMaui`, `SingleProject`) |
| Interface | Blazor Hybrid, Razor Components e XAML |
| Linguagem | C# com nullable reference types e implicit usings habilitados |
| Persistência | Entity Framework Core 10 com provider SQL Server |
| Acesso a dados adicional | Dapper 2 (referenciado no projeto) |
| Injeção de dependência | `Microsoft.Extensions.DependencyInjection` configurado em `MauiProgram` |
| Alvos declarados | Android, iOS, Mac Catalyst e Windows |

As versões exatas dos pacotes estão declaradas em `RPGdeMesa.csproj`; a versão do MAUI é controlada pela propriedade `$(MauiVersion)` fornecida pelo SDK/workload instalado.

## Arquitetura

O código está dividido em quatro camadas principais, além da inicialização do cliente:

1. **Domain** — entidades, value objects, contratos de repositório e regras de domínio. Exemplos: `Personagem`, `Campanha`, `PontosDeVida` e `SessaoMesa`.
2. **Application** — serviços que implementam casos de uso e coordenam regras, entidades e persistência. Exemplos: `CampanhaService`, `PersonagemService` e `SessaoService`.
3. **Infrastructure** — `AppDbContext` do EF Core e implementações concretas dos repositórios SQL.
4. **Presentation** — páginas Razor, componentes, layouts, ViewModels e serviços de apoio à interface (autenticação, estado de sessão e cache).
5. **MAUI host** — `App`, `MainPage` e `MauiProgram` iniciam a janela e registram o `BlazorWebView` e as dependências.

### Fluxo de uma operação

```text
Página Razor → ViewModel/serviço de apresentação → serviço de aplicação
             → contrato de repositório (Domain) → repositório (Infrastructure)
             → AppDbContext / SQL Server
```

O registro de dependências está centralizado em `Presentation/MauiProgram.cs`. Repositórios, serviços e ViewModels são majoritariamente transitórios; serviços de estado de usuário e cache são singleton; os componentes associados à sessão usam escopo conforme registrado.

## Estrutura do repositório

```text
Application/Services/          Casos de uso e serviços de aplicação
Domain/                         Entidades, contratos, exceções e value objects
Infrastructure/Persistence/    Contexto EF Core e mapeamento relacional
Infrastructure/Repositories/   Implementações dos repositórios
Presentation/Components/       Páginas Razor, layouts e componentes
Presentation/ViewModel/        Estado e comandos das telas
Presentation/Services/         Autenticação, cache e estado auxiliar da UI
Platforms/                      Inicialização e configurações específicas da plataforma
Resources/                      Ícones, fontes, imagens e recursos MAUI
wwwroot/                        Recursos estáticos da interface WebView
RPGdeMesa.csproj                Configuração do projeto e dependências NuGet
RPGdeMesa.slnx                  Solução .NET
```

## Telas e rotas

| Rota | Página | Finalidade observada |
|---|---|---|
| `/`, `/login` | Login | Autenticação |
| `/cadastro` | Register | Cadastro |
| `/home` | Home | Página inicial autenticada |
| `/perfil` | Perfil | Visualização/edição de perfil |
| `/campanhas` | Campanhas | Listagem de campanhas |
| `/criar-campanha` | CriarCampanha | Criação de campanha |
| `/entrar-cod` | PopupEntrar | Entrada em campanha por código |
| `/BibliotecaGeral` | BibliotecaGeral | Biblioteca geral de conteúdo |
| `/personagem-lib` | PersonagemLib | Biblioteca/listagem de personagens e conteúdo associado |
| `/personagem-criar` | PersonagemCriar | Criação de personagem |
| `/personagem-editar/{Id:int}` | PersonagemEditar | Edição da ficha indicada pelo ID |
| `/monstro-criar` | MonstroCriar | Cadastro de monstro |
| `/npc-criar` | NpcCriar | Cadastro de NPC |
| `/item-criar` | ItemCriar | Cadastro de item |
| `/sessao-iniciar/{idCampanha:int}` | SessaoIniciar | Preparação/início de sessão da campanha |
| `/sessao-entrar`, `/sessao-entrar/{IdCampanha:int}` | SessaoEntrar | Entrada em sessão |
| `/sessao/{IdCampanha:int}` | Sessao | Mesa da campanha |
| `/sessao/{IdCampanha:int}/{IdPersonagem:int}` | Sessao | Mesa com personagem selecionado |
| `/not-found` | NotFound | Página de rota não encontrada |

O roteador está em `Presentation/Components/Routes.razor`; páginas não encontradas também recebem um layout de fallback.

## Modelo de dados

O `AppDbContext` expõe conjuntos EF Core para as seguintes áreas:

- **Contas:** `Usuario` e `Perfil`.
- **Campanhas:** `Campanha`, `UsuarioCampanha` e associações de campanha para personagens, monstros, NPCs, itens e mapas.
- **Personagens:** `Personagem`, `Raca`, `Antecedente`, `Classe`, `PersonagemClasse`, `PersonagemSalvaguarda`, `PersonagemPericia`, `PersonagemMagia`, `Inventario` e `InventarioItem`.
- **Conteúdo:** `Monstro`, `NPC`, `Item`, `Magia` e `Mapa`.
- **Sessões:** `SessaoMesa`, `SessaoParticipante`, `SessaoMensagem` e `SessaoPino`.

As entidades são mapeadas para tabelas com nomes singulares ou plurais definidos explicitamente em `OnModelCreating`. O modelo inclui chaves e relações como conta–perfil, conta–personagens, mestre–campanha, campanha–participantes e sessão–mensagens/pinos. As propriedades relacionais e algumas tabelas de associação devem ser consultadas diretamente no `AppDbContext` antes de alterações de esquema.

O repositório não contém, na estrutura examinada, uma pasta de migrations do EF Core ou um script de criação/versionamento do banco. A aplicação pressupõe uma base SQL Server compatível já provisionada.

## Configuração e execução

### Pré-requisitos

- Windows para compilar/depurar o alvo Windows e Visual Studio com workload **.NET Multi-platform App UI development** ou instalação equivalente do workload MAUI.
- SDK .NET 10 compatível com o projeto (`net10.0-*`).
- Workloads e SDKs da plataforma desejada para Android, iOS ou Mac Catalyst. A compilação iOS/Mac Catalyst requer ambiente Apple apropriado.
- Acesso a um SQL Server com esquema compatível com o mapeamento do `AppDbContext`.

Confirme os SDKs e workloads locais:

```powershell
dotnet --info
dotnet workload list
```

Restaure e compile a solução para o alvo desejado:

```powershell
dotnet restore .\RPGdeMesa.slnx
dotnet build .\RPGdeMesa.csproj -f net10.0-windows10.0.19041.0
```

Para compilar Android, use `-f net10.0-android`; para iOS e Mac Catalyst, use `-f net10.0-ios` ou `-f net10.0-maccatalyst` em um ambiente compatível. A disponibilidade final do alvo depende do sistema operacional e dos workloads instalados. No Windows, `WindowsPackageType` está definido como `None` no projeto.

Abra `RPGdeMesa.slnx` ou `RPGdeMesa.csproj` no Visual Studio para iniciar o aplicativo com o emulador/dispositivo selecionado. Em configuração Debug, o projeto registra ferramentas de desenvolvimento do Blazor WebView e logging de depuração.

## Persistência e configuração do banco

O provider é SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`). `AppDbContext.OnConfiguring` define atualmente uma string de conexão diretamente no código como fallback quando nenhuma opção é configurada por injeção. A string não é reproduzida nesta documentação.

**Antes de distribuir ou publicar o aplicativo:** remova segredos do código-fonte e injete a configuração por um mecanismo apropriado ao ambiente/plataforma (por exemplo, secret store no desenvolvimento e configuração protegida na implantação). Se as credenciais atualmente presentes no código já foram compartilhadas fora do ambiente controlado, rotacione-as. O repositório não oferece atualmente uma seção de configuração externa documentada nem migrations incluídas.

O contexto ativa tentativas de repetição de conexão em falhas transitórias do SQL Server (até cinco tentativas, com atraso máximo configurado de cinco segundos). A configuração concreta do schema, nomes de colunas e relações está em `Infrastructure/Persistence/AppDbContext.cs`.

## Fluxos principais

### Conta

O `UsuarioService` oferece cadastro, autenticação por nome de usuário ou e-mail, cadastro/autenticação social e atualização do perfil. `SessaoUsuarioService` mantém em memória os dados da conta autenticada durante a execução do aplicativo. O estado de sessão não deve ser tratado como persistência durável entre reinicializações.

### Campanhas e fichas

`CampanhaService` concentra operações de campanha, convites, associação de conteúdo e ciclo de avaliação de ficha. Uma ficha pode ser enviada para avaliação, aprovada ou recusada; o domínio representa também o reenvio de uma ficha ajustada. O serviço de personagem mantém operações da ficha e inventário, incluindo a instanciação de personagem para campanha.

### Sessão de mesa

`SessaoService` coordena entrada e saída de participantes, sincronização do estado, mapa ativo, pinos, chat, registro de rolagens e encerramento. Também disponibiliza histórico de sessões e operações de revisão pós-sessão. O estado durável é mediado pelo repositório de sessão; caches e estado de usuário da apresentação vivem no processo do aplicativo.

## Limitações conhecidas e próximos passos

- A conexão do banco está codificada como fallback em `AppDbContext`; externalizar e proteger a configuração é necessário para uma entrega segura.
- Não foram encontradas migrations ou scripts versionados de criação do banco no repositório examinado. Documente e versiona o processo de provisionamento/migração antes de novos ambientes.
- O README anterior menciona Arduino, mas não foi encontrado código de integração com hardware no projeto examinado. Essa funcionalidade precisa ser validada ou implementada separadamente.
- O projeto referencia Dapper, porém o fluxo de persistência descrito pelo contexto e pelos repositórios usa EF Core; o uso efetivo de Dapper deve ser confirmado antes de removê-lo ou documentá-lo como requisito operacional.
- Esta documentação não afirma cobertura de testes automatizados; não foi identificada uma suíte de testes na estrutura principal listada.

## Convenções para contribuição

- Preserve a separação entre domínio, casos de uso, persistência e apresentação.
- Mantenha regras e validações de negócio nos serviços de aplicação/domínio, evitando acoplá-las à página Razor.
- Registre serviços e ViewModels em `MauiProgram.cs` com o ciclo de vida adequado ao estado que mantêm.
- Atualize os mapeamentos do `AppDbContext` e a documentação de implantação sempre que o modelo relacional mudar.
- Não inclua senhas, tokens ou strings de conexão com credenciais em commits.
