using Dapper;
using MySqlConnector;
using System.Collections.Concurrent;

namespace WeaponPaints
{
	internal class WeaponSynchronization
	{
		private readonly WeaponPaintsConfig _config;
		private readonly Database _database;

		internal WeaponSynchronization(Database database, WeaponPaintsConfig config)
		{
			_database = database;
			_config = config;
		}

		internal async Task GetPlayerData(PlayerInfo? player)
		{
			try
			{
				await using var connection = await _database.GetConnectionAsync();
				
				if (_config.Additional.KnifeEnabled)
					GetKnifeFromDatabase(player, connection);
				if (_config.Additional.GloveEnabled)
					GetGloveFromDatabase(player, connection);
				if (_config.Additional.AgentEnabled)
					GetAgentFromDatabase(player, connection);
				if (_config.Additional.MusicEnabled)
					GetMusicFromDatabase(player, connection);
				if (_config.Additional.SkinEnabled)
					GetWeaponPaintsFromDatabase(player, connection);
			}
			catch (Exception ex)
			{
				// Log the exception or handle it appropriately
				Console.WriteLine($"An error occurred: {ex.Message}");
			}
		}

		private void GetKnifeFromDatabase(PlayerInfo? player, MySqlConnection connection)
		{
			try
			{
				if (!_config.Additional.KnifeEnabled || string.IsNullOrEmpty(player?.SteamId))
					return;
				const string query = "SELECT `knife`, `team` FROM `wp_player_knife` WHERE `steamid` = @steamid";
				var playerData = connection.Query<dynamic>(query, new { steamid = player.SteamId });
				var playerKnives = new ConcurrentDictionary<int, string>();
				foreach (var row in playerData)
				{
					string knife = row?.knife ?? "weapon_knife";
					int team = row?.team ?? 0;

					playerKnives[team] = (string)knife;
				}

				WeaponPaints.g_playersKnife[player.Slot] = playerKnives;						
			}
			catch (Exception ex)
			{
				Utility.Log($"An error occurred in GetKnifeFromDatabase: {ex.Message}");
			}
		}

		private void GetGloveFromDatabase(PlayerInfo? player, MySqlConnection connection)
		{
			try
			{
				if (!_config.Additional.GloveEnabled || string.IsNullOrEmpty(player?.SteamId))
					return;


					const string query = "SELECT `weapon_defindex`, `weapon_team` FROM `wp_player_gloves` WHERE `steamid` = @steamid";
					var gloveData = connection.Query<dynamic>(query, new { steamid = player.SteamId });


					var glovesInfo = new ConcurrentDictionary<int, ushort>();
					foreach (var row in gloveData)
					{
						int weaponDefIndex = row?.weapon_defindex ?? 0;
						int weaponTeam = row?.weapon_team ?? 0;

						glovesInfo[weaponTeam] = (ushort)weaponDefIndex;
					}

					WeaponPaints.g_playersGlove[player.Slot] = glovesInfo;

			}
			catch (Exception ex)
			{
				Utility.Log($"An error occurred in GetGloveFromDatabase: {ex.Message}");
			}
		}

		private void GetAgentFromDatabase(PlayerInfo? player, MySqlConnection connection)
		{
			try
			{
				if (!_config.Additional.AgentEnabled || string.IsNullOrEmpty(player?.SteamId))
					return;

				const string query = "SELECT `agent_ct`, `agent_t` FROM `wp_player_agents` WHERE `steamid` = @steamid";
				var agentData = connection.QueryFirstOrDefault<(string, string)>(query, new { steamid = player.SteamId });

				if (agentData == default) return;
				var agentCT = agentData.Item1;
				var agentT = agentData.Item2;

				if (!string.IsNullOrEmpty(agentCT) || !string.IsNullOrEmpty(agentT))
				{
					WeaponPaints.g_playersAgent[player.Slot] = (
						agentCT,
						agentT
					);
				}
			}
			catch (Exception ex)
			{
				Utility.Log($"An error occurred in GetAgentFromDatabase: {ex.Message}");
			}
		}

		private void GetWeaponPaintsFromDatabase(PlayerInfo? player, MySqlConnection connection)
		{
			try
			{
				if (!_config.Additional.SkinEnabled || player == null || string.IsNullOrEmpty(player.SteamId))
					return;

				var weaponInfos = new ConcurrentDictionary<int, WeaponInfo>();

				const string query = "SELECT * FROM `wp_player_skins` WHERE `steamid` = @steamid";
				var playerSkins = connection.Query<dynamic>(query, new { steamid = player.SteamId });

				foreach (var row in playerSkins)
				{
					int weaponDefIndex = row?.weapon_defindex ?? 0;
					int weaponPaintId = row?.weapon_paint_id ?? 0;
					float weaponWear = row?.weapon_wear ?? 0f;
					int weaponSeed = row?.weapon_seed ?? 0;
					int weaponTeam = row?.weapon_team ?? 0;


					WeaponInfo weaponInfo = new WeaponInfo
					{
						Paint = weaponPaintId,
						Seed = weaponSeed,
						Wear = weaponWear,
						Team = weaponTeam
					};

					weaponInfos[weaponDefIndex] = weaponInfo;
				}

				WeaponPaints.gPlayerWeaponsInfo[player.Slot] = weaponInfos;
			}
			catch (Exception ex)
			{
				Utility.Log($"An error occurred in GetWeaponPaintsFromDatabase: {ex.Message}");
			}
		}

		private void GetMusicFromDatabase(PlayerInfo? player, MySqlConnection connection)
		{
			try
			{
				if (!_config.Additional.MusicEnabled || string.IsNullOrEmpty(player?.SteamId))
					return;
					const string query = "SELECT `music_id`, `team` FROM `wp_player_music` WHERE `steamid` = @steamid";
                    var musicData = connection.Query<dynamic>(query, new { steamid = player.SteamId });


					var musicInfos = new ConcurrentDictionary<int, ushort>();
					foreach (var row in musicData)
					{
						int musicId = row?.music_id ?? 0;
						int team = row?.team ?? 0;

						musicInfos[team] = (ushort)musicId;
					}
					WeaponPaints.g_playersMusic[player.Slot] = musicInfos;
			}
			catch (Exception ex)
			{
				Utility.Log($"An error occurred in GetMusicFromDatabase: {ex.Message}");
			}
		}



		internal async Task SyncKnifeToDatabase(PlayerInfo player, string knife, int team)
		{
			if (!_config.Additional.KnifeEnabled || string.IsNullOrEmpty(player.SteamId) || string.IsNullOrEmpty(knife)) return;

			const string query = "INSERT INTO `wp_player_knife` (`steamid`, `knife`, `team`) VALUES(@steamid, @newKnife, @team) ON DUPLICATE KEY UPDATE `knife` = @newKnife, `team` = @team";
			
			try
			{
				await using var connection = await _database.GetConnectionAsync();
				await connection.ExecuteAsync(query, new { steamid = player.SteamId, newKnife = knife, team = team });
			}
			catch (Exception e)
			{
				Utility.Log($"Error syncing knife to database: {e.Message}");
			}
		}

		internal async Task SyncGloveToDatabase(PlayerInfo player, int defindex, int team)
		{
			if (!_config.Additional.GloveEnabled || string.IsNullOrEmpty(player.SteamId)) return;

			try
			{
				await using var connection = await _database.GetConnectionAsync();
				const string query = "INSERT INTO `wp_player_gloves` (`steamid`, `weapon_defindex`, `weapon_team`) VALUES(@steamid, @weapon_defindex, @team) ON DUPLICATE KEY UPDATE `weapon_defindex` = @weapon_defindex, `weapon_team` = @team";
				await connection.ExecuteAsync(query, new { steamid = player.SteamId, weapon_defindex = defindex, team });

			}
			catch (Exception e)
			{
				Utility.Log($"Error syncing glove to database: {e.Message}");
			}
		}

		internal async Task SyncAgentToDatabase(PlayerInfo player)
		{
			if (!_config.Additional.AgentEnabled || string.IsNullOrEmpty(player.SteamId)) return;

			const string query = """
			                     					INSERT INTO `wp_player_agents` (`steamid`, `agent_ct`, `agent_t`)
			                     					VALUES(@steamid, @agent_ct, @agent_t)
			                     					ON DUPLICATE KEY UPDATE
			                     						`agent_ct` = @agent_ct,
			                     						`agent_t` = @agent_t
			                     """;
			try
			{
				await using var connection = await _database.GetConnectionAsync();

				await connection.ExecuteAsync(query, new { steamid = player.SteamId, agent_ct = WeaponPaints.g_playersAgent[player.Slot].CT, agent_t = WeaponPaints.g_playersAgent[player.Slot].T });
			}
			catch (Exception e)
			{
				Utility.Log($"Error syncing agents to database: {e.Message}");
			}
		}

		internal async Task SyncWeaponPaintsToDatabase(PlayerInfo player, int team)
		{
			if (string.IsNullOrEmpty(player.SteamId) || !WeaponPaints.gPlayerWeaponsInfo.TryGetValue(player.Slot, out var weaponsInfo))
				return;

			try
			{
				await using var connection = await _database.GetConnectionAsync();

				foreach (var (weaponDefIndex, weaponInfo) in weaponsInfo)
				{
					var paintId = weaponInfo.Paint;
					var wear = weaponInfo.Wear;
					var seed = weaponInfo.Seed;

					const string queryCheckExistence = "SELECT COUNT(*) FROM `wp_player_skins` WHERE `steamid` = @steamid AND `weapon_defindex` = @weaponDefIndex AND `weapon_team` = @team";

					var existingRecordCount = await connection.ExecuteScalarAsync<int>(queryCheckExistence, new { steamid = player.SteamId, weaponDefIndex = weaponDefIndex, team = team});

					string query;
					object parameters;

					if (existingRecordCount > 0)
					{
						query = "UPDATE `wp_player_skins` SET `weapon_paint_id` = @paintId, `weapon_wear` = @wear, `weapon_seed` = @seed WHERE `steamid` = @steamid AND `weapon_defindex` = @weaponDefIndex AND `weaponteam` = @team";
						parameters = new { steamid = player.SteamId, weaponDefIndex = weaponDefIndex, paintId, wear, seed, team};
					}
					else
					{
					query = "INSERT INTO `wp_player_skins` (`steamid`, `weapon_defindex`, `weapon_paint_id`, `weapon_wear`, `weapon_seed`, `weapon_team`) " +
							"VALUES (@steamid, @weaponDefIndex, @paintId, @wear, @seed, @team)";
					parameters = new { steamid = player.SteamId, weaponDefIndex = weaponDefIndex, paintId, wear, seed, team};
					}

					await connection.ExecuteAsync(query, parameters);
				}
			}
			catch (Exception e)
			{
				Utility.Log($"Error syncing weapon paints to database: {e.Message}");
			}
		}

		internal async Task SyncMusicToDatabase(PlayerInfo player, ushort music)
		{
			if (!_config.Additional.MusicEnabled || string.IsNullOrEmpty(player.SteamId)) return;

			try
			{
				await using var connection = await _database.GetConnectionAsync();
				const string query = "INSERT INTO `wp_player_music` (`steamid`, `music_id`) VALUES(@steamid, @newMusic) ON DUPLICATE KEY UPDATE `music_id` = @newMusic";
				await connection.ExecuteAsync(query, new { steamid = player.SteamId, newMusic = music });
			}
			catch (Exception e)
			{
				Utility.Log($"Error syncing music kit to database: {e.Message}");
			}
		}
	}
}