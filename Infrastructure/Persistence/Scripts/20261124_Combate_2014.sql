SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.Sessao_Mesa', N'U') IS NULL OR OBJECT_ID(N'dbo.Sessao_Pino', N'U') IS NULL
    THROW 51003, 'Selecione o banco da aplicacao que contem Sessao_Mesa e Sessao_Pino.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Sessao_Mesa', N'combate_2014_json') IS NULL
    ALTER TABLE dbo.Sessao_Mesa ADD combate_2014_json nvarchar(max) NULL;

IF COL_LENGTH(N'dbo.Sessao_Pino', N'instancia') IS NULL
    ALTER TABLE dbo.Sessao_Pino ADD instancia int NOT NULL CONSTRAINT DF_SessaoPino_Instancia DEFAULT (1);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Sessao_Pino') AND name = N'IX_SessaoPino_Sessao_Mapa_Instancia')
    EXEC(N'CREATE UNIQUE INDEX IX_SessaoPino_Sessao_Mapa_Instancia ON dbo.Sessao_Pino (id_sessao, id_mapa, tipo_entidade, id_entidade, instancia)');

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Sessao_Pino') AND name = N'IX_SessaoPino_Sessao_Mapa_Entidade')
    DROP INDEX IX_SessaoPino_Sessao_Mapa_Entidade ON dbo.Sessao_Pino;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Sessao_Pino') AND name = N'IX_Sessao_Pino_id_sessao_id_mapa_tipo_entidade_id_entidade')
    DROP INDEX IX_Sessao_Pino_id_sessao_id_mapa_tipo_entidade_id_entidade ON dbo.Sessao_Pino;

COMMIT TRANSACTION;
