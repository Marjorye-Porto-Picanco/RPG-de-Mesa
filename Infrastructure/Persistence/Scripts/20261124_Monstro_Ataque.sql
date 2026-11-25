SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF OBJECT_ID(N'[dbo].[Monstro_Ataque]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Monstro_Ataque] (
        [id_monstro_ataque] int IDENTITY(1,1) NOT NULL,
        [id_monstro] int NOT NULL,
        [nome] varchar(100) NOT NULL,
        [bonus_ataque] int NULL,
        [dano] varchar(30) NULL,
        [tipo_dano] varchar(30) NULL,
        [alcance] varchar(50) NULL,
        [descricao] varchar(max) NULL,
        CONSTRAINT [PK_Monstro_Ataque] PRIMARY KEY CLUSTERED ([id_monstro_ataque] ASC),
        CONSTRAINT [FK_Monstro_Ataque_Monstro] FOREIGN KEY ([id_monstro]) REFERENCES [dbo].[Monstro] ([id_monstro]) ON DELETE CASCADE
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
    CREATE NONCLUSTERED INDEX [IX_Monstro_Ataque_id_monstro] ON [dbo].[Monstro_Ataque] ([id_monstro] ASC);
END
GO
IF COL_LENGTH('dbo.NPC', 'pv_maximo') IS NULL
BEGIN
    ALTER TABLE [dbo].[NPC] ADD [pv_maximo] int NOT NULL CONSTRAINT [DF_NPC_pv_maximo] DEFAULT ((10)), [classe_armadura] int NOT NULL CONSTRAINT [DF_NPC_ca] DEFAULT ((10)), [deslocamento] int NOT NULL CONSTRAINT [DF_NPC_deslocamento] DEFAULT ((9));
END
GO
IF OBJECT_ID(N'[dbo].[NPC_Magia]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NPC_Magia] ([id_npc] int NOT NULL, [id_magia] int NOT NULL,
        CONSTRAINT [PK_NPC_Magia] PRIMARY KEY CLUSTERED ([id_npc] ASC, [id_magia] ASC),
        CONSTRAINT [FK_NPCMag_NPC] FOREIGN KEY ([id_npc]) REFERENCES [dbo].[NPC] ([id_npc]) ON DELETE CASCADE,
        CONSTRAINT [FK_NPCMag_Magia] FOREIGN KEY ([id_magia]) REFERENCES [dbo].[Magia] ([id_magia]) ON DELETE CASCADE) ON [PRIMARY];
END
GO
IF OBJECT_ID(N'[dbo].[NPC_Item]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NPC_Item] ([id_npc] int NOT NULL, [id_item] int NOT NULL,
        [quantidade] int NOT NULL CONSTRAINT [DF_NPCItem_Quantidade] DEFAULT ((1)),
        CONSTRAINT [PK_NPC_Item] PRIMARY KEY CLUSTERED ([id_npc] ASC, [id_item] ASC),
        CONSTRAINT [FK_NPCItem_NPC] FOREIGN KEY ([id_npc]) REFERENCES [dbo].[NPC] ([id_npc]) ON DELETE CASCADE,
        CONSTRAINT [FK_NPCItem_Item] FOREIGN KEY ([id_item]) REFERENCES [dbo].[Item] ([id_item]) ON DELETE CASCADE) ON [PRIMARY];
END
GO
IF OBJECT_ID(N'[dbo].[Personagem_Circulo_Magico]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Personagem_Circulo_Magico] ([id_personagem] int NOT NULL, [circulo_nivel] int NOT NULL,
        [total_slots] int NOT NULL CONSTRAINT [DF_PCirc_Total] DEFAULT ((0)), [slots_usados] int NOT NULL CONSTRAINT [DF_PCirc_Usados] DEFAULT ((0)), [total_slots_pendente] int NULL,
        CONSTRAINT [PK_Personagem_Circulo_Magico] PRIMARY KEY CLUSTERED ([id_personagem] ASC, [circulo_nivel] ASC),
        CONSTRAINT [FK_PCirc_Personagem] FOREIGN KEY ([id_personagem]) REFERENCES [dbo].[Personagem] ([id_personagem]) ON DELETE CASCADE) ON [PRIMARY];
END
GO
IF COL_LENGTH('dbo.Sessao_Participante', 'acoes_restantes') IS NULL
    ALTER TABLE [dbo].[Sessao_Participante] ADD [acoes_restantes] int NOT NULL CONSTRAINT [DF_SessaoPart_Acoes] DEFAULT ((1));
IF COL_LENGTH('dbo.Sessao_Participante', 'deslocamento_usado') IS NULL
    ALTER TABLE [dbo].[Sessao_Participante] ADD [deslocamento_usado] decimal(5,2) NOT NULL CONSTRAINT [DF_SessaoPart_Deslocamento] DEFAULT ((0));
IF COL_LENGTH('dbo.Sessao_Participante', 'id_magia_conjurando') IS NULL
    ALTER TABLE [dbo].[Sessao_Participante] ADD [id_magia_conjurando] int NULL;
IF COL_LENGTH('dbo.Sessao_Participante', 'turnos_conjuracao_restantes') IS NULL
    ALTER TABLE [dbo].[Sessao_Participante] ADD [turnos_conjuracao_restantes] int NOT NULL CONSTRAINT [DF_SessaoPart_TurnosConj] DEFAULT ((0));
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SessaoPart_MagiaConjurando')
    ALTER TABLE [dbo].[Sessao_Participante] WITH CHECK ADD CONSTRAINT [FK_SessaoPart_MagiaConjurando] FOREIGN KEY ([id_magia_conjurando]) REFERENCES [dbo].[Magia] ([id_magia]);
GO
/* Existing free-text features have no attack values in the supplied schema. */
IF COL_LENGTH('dbo.Monstro', 'habilidades') IS NOT NULL
    INSERT INTO [dbo].[Monstro_Ataque] ([id_monstro], [nome], [descricao])
    SELECT m.[id_monstro], 'Ataque legado', NULLIF(LTRIM(RTRIM(m.[habilidades])), '')
    FROM [dbo].[Monstro] m WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Monstro_Ataque] a WHERE a.[id_monstro] = m.[id_monstro]);
GO
