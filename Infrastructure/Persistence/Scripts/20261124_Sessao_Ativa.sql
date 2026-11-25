SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF EXISTS (
    SELECT id_campanha
    FROM dbo.Sessao_Mesa WITH (UPDLOCK, HOLDLOCK)
    WHERE ativa = 1
    GROUP BY id_campanha
    HAVING COUNT(*) > 1
)
    THROW 51002, 'Ha campanhas com mais de uma sessao ativa. Revise os registros antes de aplicar o indice.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Sessao_Mesa')
      AND name = N'UX_Sessao_Mesa_Campanha_Ativa'
)
    CREATE UNIQUE INDEX UX_Sessao_Mesa_Campanha_Ativa
        ON dbo.Sessao_Mesa (id_campanha)
        WHERE ativa = 1;

IF OBJECT_ID(N'dbo.Sessao_Operacao', N'U') IS NULL
    CREATE TABLE dbo.Sessao_Operacao (
        id_sessao int NOT NULL,
        id_operacao uniqueidentifier NOT NULL,
        CONSTRAINT PK_Sessao_Operacao PRIMARY KEY (id_sessao, id_operacao),
        CONSTRAINT FK_Sessao_Operacao_Sessao FOREIGN KEY (id_sessao)
            REFERENCES dbo.Sessao_Mesa (id_sessao) ON DELETE CASCADE
    );

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Sessao_Mensagem')
      AND name = N'IX_Sessao_Mensagem_Sessao_Id'
)
    CREATE INDEX IX_Sessao_Mensagem_Sessao_Id
        ON dbo.Sessao_Mensagem (id_sessao, id_mensagem);

COMMIT TRANSACTION;
