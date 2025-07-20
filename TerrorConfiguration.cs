using System.Collections.Generic;

namespace ToNStatTool
{
	/// <summary>
	/// テラーのスタン可否設定を管理するクラス
	/// </summary>
	public static class TerrorConfiguration
	{
		/// <summary>
		/// テラー名とスタン可否タイプのマッピング辞書（JSONデータが無い場合のフォールバック用）
		/// </summary>
		public static readonly Dictionary<string, TerrorStunType> StunConfig = new Dictionary<string, TerrorStunType>
		{
            // スタン厳禁 (赤) - 凶悪なカウンター持ち
            {"Atrached", TerrorStunType.Forbidden},
			{"Charlotte", TerrorStunType.Forbidden},         // 中身が出てきて、攻撃範囲が大きく広がり即死攻撃
            {"Don't Touch Me", TerrorStunType.Forbidden},
			{"Haket", TerrorStunType.Forbidden},             // 5回スタンすると移動速度が上がる
            {"Hell Bell", TerrorStunType.Forbidden},         // 鐘を鳴らす攻撃を誘発する
            {"MopeMope", TerrorStunType.Forbidden},          // 即発狂
            {"Punishing Bird", TerrorStunType.Forbidden},    // 15秒間攻撃が即死に
            {"Specimen 10", TerrorStunType.Forbidden},       // イモムシモードになって加速し、チェイスの邪魔になる
            {"Sturm", TerrorStunType.Forbidden},             // 長時間加速モードになり、その間接触即死に
            {"Tricky", TerrorStunType.Forbidden},            // 発狂モードに入りプレイヤーをキルするまで速度が大幅に上昇
            {"V2", TerrorStunType.Forbidden},                // 発狂モードになり加速する
            {"Apathy", TerrorStunType.Forbidden},
			{"This Killer Does Not Exist", TerrorStunType.Forbidden},	// Apathyと同じ
			{"Try Not To Touch Me", TerrorStunType.Forbidden},
			{"Nameless", TerrorStunType.Forbidden},
			{"Rewrite", TerrorStunType.Forbidden},
			{"Blue Haket", TerrorStunType.Forbidden},
			{"Toren's Shadow", TerrorStunType.Forbidden},    // 一定回数で発狂し、速度が大幅に上がり攻撃が即死に
            {"Purple Guy", TerrorStunType.Forbidden},        // 即死攻撃をしてくる

            // 注意が必要 (黄) - 条件付きでスタン可能
            {"Dr. Tox", TerrorStunType.Caution},             // 1,2回目は○、3回目から◎
            {"Yolm", TerrorStunType.Caution},
			{"The Batter", TerrorStunType.Caution},
			{"Pandora", TerrorStunType.Caution},
			{"Roblander", TerrorStunType.Caution},
			{"Inverted Roblander", TerrorStunType.Caution},
			{"Arrival", TerrorStunType.Caution},

            // スタン推奨/有効 (緑) - メリットあり
            {"Corrupted Toys", TerrorStunType.Safe},
			{"Sawrunner", TerrorStunType.Safe},
			{"Demented Spongebob", TerrorStunType.Safe},
			{"Dog Mimic", TerrorStunType.Safe},
			{"Ao Oni", TerrorStunType.Safe},
			{"Tails Doll", TerrorStunType.Safe},             // 88〜48秒までの40秒間はスタン不可
            {"Black Sun", TerrorStunType.Safe},              // 手下のみ、本体はスタン不可
            {"CENSORED", TerrorStunType.Safe},
			{"WhiteNight", TerrorStunType.Safe},             // 眷族のみ
            {"Starved", TerrorStunType.Safe},
			{"The Painter", TerrorStunType.Safe},            // 絵のみ、本体はスタン不可
            {"with many voices", TerrorStunType.Safe},
			{"Karol_Corpse", TerrorStunType.Safe},           // 頭が発光してる時はスタン不可
            {"MX", TerrorStunType.Safe},                     // 発狂後スタン不可
            {"Dev bytes", TerrorStunType.Safe},
			{"Withered Bonnie", TerrorStunType.Safe},
			{"The Boys", TerrorStunType.Safe},
			{"Seek", TerrorStunType.Safe},
			{"Sonic", TerrorStunType.Safe},                  // Fakerの場合のみ
            {"Bad batter", TerrorStunType.Safe},
			{"Mirror", TerrorStunType.Safe},
			{"Legs", TerrorStunType.Safe},
			{"Mona & The Mountain", TerrorStunType.Safe},    // 山の方
            {"Garten Goers", TerrorStunType.Safe},           // オピラバードとバンバンのみ、他はスタン不可
            {"Specimen2", TerrorStunType.Safe},
			{"Specimen 2", TerrorStunType.Safe},
			{"Pale Association", TerrorStunType.Safe},
			{"Toy Enforcer", TerrorStunType.Safe},
			{"TBH", TerrorStunType.Safe},
			{"Doombox", TerrorStunType.Safe},
			{"Apocrean Harvester", TerrorStunType.Safe},     // 触手のみ、本体はスタン不可
            {"Arkus", TerrorStunType.Safe},
			{"Cartoon Cat", TerrorStunType.Safe},
			{"Shinto", TerrorStunType.Safe},
			{"BFF", TerrorStunType.Safe},
			{"Security", TerrorStunType.Safe},
			{"The Swarm", TerrorStunType.Safe},
			{"Shiteyanyo", TerrorStunType.Safe},
			{"Bacteria", TerrorStunType.Safe},
			{"HoovyDundy", TerrorStunType.Safe},
			{"Lunatic Cultist", TerrorStunType.Safe},
			{"Prisoner", TerrorStunType.Safe},               // 残り60秒からスタン不可
            {"All-Around-Helpers", TerrorStunType.Safe},
			{"Sakuya The Ripper", TerrorStunType.Safe},
			{"Sakuya Izayoi", TerrorStunType.Safe},
			{"Miros Birds", TerrorStunType.Safe},
			{"Ink Demon", TerrorStunType.Safe},
			{"Retep", TerrorStunType.Safe},
			{"Those Olden Days", TerrorStunType.Safe},       // テレビのみ、本体はスタン不可
            {"Olden Days", TerrorStunType.Safe},        // テレビのみ、本体はスタン不可
            {"Spamton", TerrorStunType.Safe},
			{"Wild Yet Curious Creature", TerrorStunType.Safe},
			{"Manti", TerrorStunType.Safe},
			{"Cubor's Revenge", TerrorStunType.Safe},        // オレンジ色のキューブのみ、本体はスタン不可
            {"Origin", TerrorStunType.Safe},
			{"Beyond", TerrorStunType.Safe},
			{"ToN", TerrorStunType.Safe},
			{"poly", TerrorStunType.Safe},
			{"ドッグミミック", TerrorStunType.Safe},          // 開いてるときのみ
            {"FOX Squad", TerrorStunType.Safe},
			{"Malicious Twins", TerrorStunType.Safe},
			{"Parhelion's Victims", TerrorStunType.Safe},    // ミートボールみたいなやつとワームみたいなやつはスタン不可
            {"Bravera", TerrorStunType.Safe},
			{"MissingNo", TerrorStunType.Safe},              // カブトプスとプテラの形態時のみ、他形態はスタン不可
            {"Living Shadow", TerrorStunType.Safe},
			{"ペスト医師", TerrorStunType.Safe},              // ゾンビのみ、本体はスタン不可
            {"Clockey", TerrorStunType.Safe},                // ハァハァって煽ってくるときのみ
            {"Terror of Nowhere", TerrorStunType.Safe},
			{"Christian Brutal Sniper", TerrorStunType.Safe},

			{"The LifeBringer", TerrorStunType.Unknown},
			{"Tragedy", TerrorStunType.Unknown},
			{"The Observation", TerrorStunType.Unknown},
			{"S.T.G.M", TerrorStunType.Unknown},
			{"Monarch", TerrorStunType.Unknown},
			{"Express Train To Hell", TerrorStunType.Unknown},
			{"Parhelion", TerrorStunType.Unknown},
			{"Virus", TerrorStunType.Unknown}
		};

		/// <summary>
		/// テラー名からスタン可否タイプを取得する（JSON優先）
		/// </summary>
		public static TerrorStunType GetTerrorStunType(string terrorName)
		{
			// まずJSONデータから取得を試行
			var terrorDetail = TerrorJsonLoader.GetTerrorDetail(terrorName);
			if (terrorDetail != null && terrorDetail.StunType != TerrorStunType.Unknown)
			{
				return terrorDetail.StunType;
			}

			// JSONにスタン情報がない場合、設定辞書から取得（大文字小文字を区別しない）
			foreach (var kvp in StunConfig)
			{
				if (string.Equals(kvp.Key, terrorName, System.StringComparison.OrdinalIgnoreCase))
				{
					return kvp.Value;
				}
			}

			// Convict Squadの特別処理
			if (terrorName.IndexOf("Convict Squad", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				if (terrorName.IndexOf("Yellow", System.StringComparison.OrdinalIgnoreCase) >= 0)
					return TerrorStunType.Caution;
				else
					return TerrorStunType.Forbidden;
			}

			// デフォルトはスタン可否不明
			return TerrorStunType.Unknown;
		}
	}
}