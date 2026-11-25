-- Script desativado enquanto o cadastro de ataques de NPC estiver comentado.

-- SET ANSI_NULLS ON
-- GO
-- SET QUOTED_IDENTIFIER ON
-- GO

-- IF OBJECT_ID(N'[dbo].[NPC_Ataque]', N'U') IS NULL
-- BEGIN
--     CREATE TABLE [dbo].[NPC_Ataque] (
--         [id_npc_ataque] int IDENTITY(1,1) NOT NULL,
--         [id_npc] int NOT NULL,
--         [nome] varchar(100) NOT NULL,
--         [bonus_ataque] int NULL,
--         [dano] varchar(30) NULL,
--         [tipo_dano] varchar(30) NULL,
--         [alcance] varchar(50) NULL,
--         [descricao] varchar(max) NULL,
--         CONSTRAINT [PK_NPC_Ataque] PRIMARY KEY CLUSTERED ([id_npc_ataque]),
--         CONSTRAINT [FK_NPC_Ataque_NPC] FOREIGN KEY ([id_npc]) REFERENCES [dbo].[NPC] ([id_npc]) ON DELETE CASCADE
--     );
-- END
-- GO

-- IF NOT EXISTS (
--     SELECT 1 FROM sys.indexes
--     WHERE object_id = OBJECT_ID(N'[dbo].[NPC_Ataque]', N'U') AND name = N'IX_NPC_Ataque_id_npc'
-- )
--     CREATE NONCLUSTERED INDEX [IX_NPC_Ataque_id_npc] ON [dbo].[NPC_Ataque] ([id_npc]);
-- GO
