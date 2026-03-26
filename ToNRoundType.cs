using System;
using System.Collections.Generic;

namespace ToNStatTool
{
    /// <summary>
    /// Terror of Nowhereのラウンドタイプを定義するEnum
    /// ToNSaveManagerの定義に準拠
    /// </summary>
    public enum ToNRoundType
    {
        /// <summary>インターミッション（ラウンド間）</summary>
        Intermission = 0,

        // === Normal Rounds (通常ラウンド) ===
        /// <summary>クラシック</summary>
        Classic = 1,
        /// <summary>フォグ</summary>
        Fog = 2,
        /// <summary>パニッシュド</summary>
        Punished = 3,
        /// <summary>サボタージュ</summary>
        Sabotage = 4,
        /// <summary>クラックド</summary>
        Cracked = 5,
        /// <summary>ブラッドバス</summary>
        Bloodbath = 6,
        /// <summary>ダブルトラブル（2体のテラーが同一ID）</summary>
        Double_Trouble = 7,
        /// <summary>EX（全テラーが同一ID）</summary>
        EX = 8,
        /// <summary>ゴースト</summary>
        Ghost = 9,
        /// <summary>アンバウンド</summary>
        Unbound = 10,

        // === Alternates (オルタネート系) ===
        /// <summary>ミッドナイト</summary>
        Midnight = 50,
        /// <summary>オルタネート</summary>
        Alternate = 51,
        /// <summary>フォグオルタネート</summary>
        Fog_Alternate = 52,
        /// <summary>ゴーストオルタネート</summary>
        Ghost_Alternate = 53,

        // === Moons (ムーン系) ===
        /// <summary>ミスティックムーン</summary>
        Mystic_Moon = 100,
        /// <summary>ブラッドムーン</summary>
        Blood_Moon = 101,
        /// <summary>トワイライト</summary>
        Twilight = 102,
        /// <summary>ソルスティス</summary>
        Solstice = 103,

        // === Specials (スペシャル系) ===
        /// <summary>RUN</summary>
        RUN = 104,
        /// <summary>8ページ</summary>
        Eight_Pages = 105,
        /// <summary>ギガバイト（エイプリルフール）</summary>
        GIGABYTE = 106,
        /// <summary>コールドナイト（ウィンターフェスト）</summary>
        Cold_Night = 107,

        /// <summary>カスタム/不明</summary>
        Custom = 999
    }

    /// <summary>
    /// ToNRoundTypeに関するヘルパーメソッドを提供するstaticクラス
    /// </summary>
    public static class ToNRoundTypeHelper
    {
        // 文字列→Enum変換用のマッピング辞書（大文字小文字を区別しない）
        private static readonly Dictionary<string, ToNRoundType> NameToTypeMap;

        // Enum→表示名変換用のマッピング辞書
        private static readonly Dictionary<ToNRoundType, string> TypeToDisplayNameMap;

        /// <summary>
        /// 静的コンストラクタでマッピングを初期化
        /// </summary>
        static ToNRoundTypeHelper()
        {
            // 文字列→Enumマッピング（英語・日本語両対応）
            NameToTypeMap = new Dictionary<string, ToNRoundType>(StringComparer.OrdinalIgnoreCase)
            {
                // Intermission
                { "intermission", ToNRoundType.Intermission },
                { "インターミッション", ToNRoundType.Intermission },

                // Normal Rounds
                { "classic", ToNRoundType.Classic },
                { "クラシック", ToNRoundType.Classic },
                
                { "fog", ToNRoundType.Fog },
                { "フォグ", ToNRoundType.Fog },
                
                { "punished", ToNRoundType.Punished },
                { "パニッシュド", ToNRoundType.Punished },
                
                { "sabotage", ToNRoundType.Sabotage },
                { "サボタージュ", ToNRoundType.Sabotage },
                
                { "cracked", ToNRoundType.Cracked },
                { "クラックド", ToNRoundType.Cracked },
                
                { "bloodbath", ToNRoundType.Bloodbath },
                { "ブラッドバス", ToNRoundType.Bloodbath },
                
                { "double trouble", ToNRoundType.Double_Trouble },
                { "double_trouble", ToNRoundType.Double_Trouble },
                { "ダブルトラブル", ToNRoundType.Double_Trouble },
                
                { "ex", ToNRoundType.EX },
                
                { "ghost", ToNRoundType.Ghost },
                { "ゴースト", ToNRoundType.Ghost },
                
                { "unbound", ToNRoundType.Unbound },
                { "アンバウンド", ToNRoundType.Unbound },

                // Alternates
                { "midnight", ToNRoundType.Midnight },
                { "ミッドナイト", ToNRoundType.Midnight },
                
                { "alternate", ToNRoundType.Alternate },
                { "オルタネート", ToNRoundType.Alternate },
                
                { "fog alternate", ToNRoundType.Fog_Alternate },
                { "fog_alternate", ToNRoundType.Fog_Alternate },
                { "fog (alternate)", ToNRoundType.Fog_Alternate },
                { "フォグオルタネート", ToNRoundType.Fog_Alternate },
                
                { "ghost alternate", ToNRoundType.Ghost_Alternate },
                { "ghost_alternate", ToNRoundType.Ghost_Alternate },
                { "ghost (alternate)", ToNRoundType.Ghost_Alternate },
                { "ゴーストオルタネート", ToNRoundType.Ghost_Alternate },

                // Moons
                { "mystic moon", ToNRoundType.Mystic_Moon },
                { "mystic_moon", ToNRoundType.Mystic_Moon },
                { "ミスティックムーン", ToNRoundType.Mystic_Moon },
                
                { "blood moon", ToNRoundType.Blood_Moon },
                { "blood_moon", ToNRoundType.Blood_Moon },
                { "ブラッドムーン", ToNRoundType.Blood_Moon },
                
                { "twilight", ToNRoundType.Twilight },
                { "トワイライト", ToNRoundType.Twilight },
                
                { "solstice", ToNRoundType.Solstice },
                { "ソルスティス", ToNRoundType.Solstice },

                // Specials
                { "run", ToNRoundType.RUN },
                { "走れ", ToNRoundType.RUN },
                
                { "8 pages", ToNRoundType.Eight_Pages },
                { "8pages", ToNRoundType.Eight_Pages },
                { "eight pages", ToNRoundType.Eight_Pages },
                { "eight_pages", ToNRoundType.Eight_Pages },
                { "8ページ", ToNRoundType.Eight_Pages },
                
                { "gigabyte", ToNRoundType.GIGABYTE },
                { "ギガバイト", ToNRoundType.GIGABYTE },
                
                { "cold night", ToNRoundType.Cold_Night },
                { "cold_night", ToNRoundType.Cold_Night },
                { "コールドナイト", ToNRoundType.Cold_Night },

                // Custom
                { "custom", ToNRoundType.Custom },
                { "unknown", ToNRoundType.Custom }
            };

            // Enum→表示名マッピング
            TypeToDisplayNameMap = new Dictionary<ToNRoundType, string>
            {
                { ToNRoundType.Intermission, "Intermission" },
                { ToNRoundType.Classic, "Classic" },
                { ToNRoundType.Fog, "Fog" },
                { ToNRoundType.Punished, "Punished" },
                { ToNRoundType.Sabotage, "Sabotage" },
                { ToNRoundType.Cracked, "Cracked" },
                { ToNRoundType.Bloodbath, "Bloodbath" },
                { ToNRoundType.Double_Trouble, "Double Trouble" },
                { ToNRoundType.EX, "EX" },
                { ToNRoundType.Ghost, "Ghost" },
                { ToNRoundType.Unbound, "Unbound" },
                { ToNRoundType.Midnight, "Midnight" },
                { ToNRoundType.Alternate, "Alternate" },
                { ToNRoundType.Fog_Alternate, "Fog (Alternate)" },
                { ToNRoundType.Ghost_Alternate, "Ghost (Alternate)" },
                { ToNRoundType.Mystic_Moon, "Mystic Moon" },
                { ToNRoundType.Blood_Moon, "Blood Moon" },
                { ToNRoundType.Twilight, "Twilight" },
                { ToNRoundType.Solstice, "Solstice" },
                { ToNRoundType.RUN, "RUN" },
                { ToNRoundType.Eight_Pages, "8 Pages" },
                { ToNRoundType.GIGABYTE, "GIGABYTE" },
                { ToNRoundType.Cold_Night, "Cold Night" },
                { ToNRoundType.Custom, "Custom" }
            };
        }

        /// <summary>
        /// 文字列からToNRoundTypeに変換を試みる
        /// </summary>
        /// <param name="name">ラウンドタイプ名（英語/日本語）</param>
        /// <param name="result">変換結果</param>
        /// <returns>変換に成功した場合true</returns>
        public static bool TryParse(string name, out ToNRoundType result)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                result = ToNRoundType.Intermission;
                return false;
            }

            // 直接マッチを試みる
            if (NameToTypeMap.TryGetValue(name.Trim(), out result))
            {
                return true;
            }

            // 部分一致を試みる（優先度順）
            string lower = name.ToLower();

            // Moons (先にチェック - 特殊性が高い)
            if (lower.Contains("mystic") && lower.Contains("moon")) { result = ToNRoundType.Mystic_Moon; return true; }
            if (lower.Contains("blood") && lower.Contains("moon")) { result = ToNRoundType.Blood_Moon; return true; }
            if (lower.Contains("ブラッドムーン")) { result = ToNRoundType.Blood_Moon; return true; }
            if (lower.Contains("ミスティックムーン")) { result = ToNRoundType.Mystic_Moon; return true; }
            if (lower.Contains("twilight") || lower.Contains("トワイライト")) { result = ToNRoundType.Twilight; return true; }
            if (lower.Contains("solstice") || lower.Contains("ソルスティス")) { result = ToNRoundType.Solstice; return true; }

            // Alternates
            if ((lower.Contains("fog") || lower.Contains("フォグ")) && lower.Contains("alternate")) { result = ToNRoundType.Fog_Alternate; return true; }
            if ((lower.Contains("ghost") || lower.Contains("ゴースト")) && lower.Contains("alternate")) { result = ToNRoundType.Ghost_Alternate; return true; }
            if (lower.Contains("midnight") || lower.Contains("ミッドナイト")) { result = ToNRoundType.Midnight; return true; }
            if (lower.Contains("alternate") || lower.Contains("オルタネート")) { result = ToNRoundType.Alternate; return true; }

            // Specials
            if (lower.Contains("8 pages") || lower.Contains("8pages") || lower.Contains("8ページ")) { result = ToNRoundType.Eight_Pages; return true; }
            if (lower == "run" || lower.Contains("走れ")) { result = ToNRoundType.RUN; return true; }
            if (lower.Contains("gigabyte") || lower.Contains("ギガバイト")) { result = ToNRoundType.GIGABYTE; return true; }
            if (lower.Contains("cold night") || lower.Contains("cold_night") || lower.Contains("コールドナイト")) { result = ToNRoundType.Cold_Night; return true; }

            // Normal Rounds
            if (lower.Contains("double") && lower.Contains("trouble")) { result = ToNRoundType.Double_Trouble; return true; }
            if (lower.Contains("ダブルトラブル")) { result = ToNRoundType.Double_Trouble; return true; }
            if (lower.Contains("classic") || lower.Contains("クラシック")) { result = ToNRoundType.Classic; return true; }
            if (lower.Contains("fog") || lower.Contains("フォグ")) { result = ToNRoundType.Fog; return true; }
            if (lower.Contains("punished") || lower.Contains("パニッシュド")) { result = ToNRoundType.Punished; return true; }
            if (lower.Contains("sabotage") || lower.Contains("サボタージュ")) { result = ToNRoundType.Sabotage; return true; }
            if (lower.Contains("cracked") || lower.Contains("クラックド")) { result = ToNRoundType.Cracked; return true; }
            if (lower.Contains("bloodbath") || lower.Contains("ブラッドバス")) { result = ToNRoundType.Bloodbath; return true; }
            if (lower == "ex") { result = ToNRoundType.EX; return true; }
            if (lower.Contains("ghost") || lower.Contains("ゴースト")) { result = ToNRoundType.Ghost; return true; }
            if (lower.Contains("unbound") || lower.Contains("アンバウンド")) { result = ToNRoundType.Unbound; return true; }

            result = ToNRoundType.Custom;
            return false;
        }

        /// <summary>
        /// 整数値からToNRoundTypeに変換
        /// </summary>
        /// <param name="value">ラウンドタイプの整数値</param>
        /// <returns>対応するToNRoundType（未定義の場合はCustom）</returns>
        public static ToNRoundType FromInt(int value)
        {
            if (Enum.IsDefined(typeof(ToNRoundType), value))
            {
                return (ToNRoundType)value;
            }
            return ToNRoundType.Custom;
        }

        /// <summary>
        /// ToNRoundTypeの表示名を取得
        /// </summary>
        /// <param name="roundType">ラウンドタイプ</param>
        /// <returns>表示名</returns>
        public static string GetDisplayName(ToNRoundType roundType)
        {
            if (TypeToDisplayNameMap.TryGetValue(roundType, out string name))
            {
                return name;
            }
            return roundType.ToString().Replace("_", " ");
        }

        /// <summary>
        /// 文字列からToNRoundTypeに変換（見つからない場合はCustom）
        /// </summary>
        /// <param name="name">ラウンドタイプ名</param>
        /// <returns>対応するToNRoundType</returns>
        public static ToNRoundType Parse(string name)
        {
            TryParse(name, out ToNRoundType result);
            return result;
        }

        // ============================================
        // ラウンド種別判定メソッド
        // ============================================

        /// <summary>
        /// 通常ラウンド（Classicスロット消費）かどうかを判定
        /// Classicのみが該当（純粋な通常枠）
        /// </summary>
        public static bool IsClassicRound(ToNRoundType roundType)
        {
            return roundType == ToNRoundType.Classic;
        }

        /// <summary>
        /// 通常ラウンド（通常スロット消費）かどうかを判定
        /// Classic, RUN, GIGABYTE が該当（周期計算で同じ扱い）
        /// ※GIGABYTEはエイプリルフールイベントでClassicの代わりに出現する
        /// </summary>
        public static bool IsNormalRound(ToNRoundType roundType)
        {
            return roundType == ToNRoundType.Classic ||
                   roundType == ToNRoundType.RUN ||
                   roundType == ToNRoundType.GIGABYTE;
        }

        /// <summary>
        /// スペシャルラウンド（特殊スロット消費）かどうかを判定
        /// Fog, Punished, Sabotage, Cracked, Bloodbath, Double_Trouble, EX, Midnight, Alternate, Fog_Alternate,
        /// Cold_Night が該当
        /// ※Fog/Punished/Sabotage/Crackedは特殊枠から1/6で選出される
        /// ※GIGABYTEはClassic上書き（通常枠消費）のためここには含まない
        /// </summary>
        public static bool IsSpecialRound(ToNRoundType roundType)
        {
            return roundType == ToNRoundType.Fog ||
                   roundType == ToNRoundType.Punished ||
                   roundType == ToNRoundType.Sabotage ||
                   roundType == ToNRoundType.Cracked ||
                   roundType == ToNRoundType.Bloodbath ||
                   roundType == ToNRoundType.Double_Trouble ||
                   roundType == ToNRoundType.EX ||
                   roundType == ToNRoundType.Midnight ||
                   roundType == ToNRoundType.Alternate ||
                   roundType == ToNRoundType.Fog_Alternate ||
                   roundType == ToNRoundType.Cold_Night;
        }

        /// <summary>
        /// オーバーライドラウンド（通常/特殊どちらも消費しない）かどうかを判定
        /// Ghost, Ghost_Alternate, Unbound, Eight_Pages が該当
        /// ※RUNは通常ラウンド扱い（IsNormalRound参照）
        /// </summary>
        public static bool IsOverrideRound(ToNRoundType roundType)
        {
            return roundType == ToNRoundType.Ghost ||
                   roundType == ToNRoundType.Ghost_Alternate ||
                   roundType == ToNRoundType.Unbound ||
                   roundType == ToNRoundType.Eight_Pages;
        }

        /// <summary>
        /// ムーンラウンドかどうかを判定
        /// Mystic_Moon, Blood_Moon, Twilight, Solstice が該当
        /// </summary>
        public static bool IsMoonRound(ToNRoundType roundType)
        {
            return roundType == ToNRoundType.Mystic_Moon ||
                   roundType == ToNRoundType.Blood_Moon ||
                   roundType == ToNRoundType.Twilight ||
                   roundType == ToNRoundType.Solstice;
        }

        /// <summary>
        /// イベントラウンドかどうかを判定
        /// GIGABYTE, Cold_Night が該当
        /// </summary>
        public static bool IsEventRound(ToNRoundType roundType)
        {
            return roundType == ToNRoundType.GIGABYTE ||
                   roundType == ToNRoundType.Cold_Night;
        }

        /// <summary>
        /// アイテムリマインダー対象ラウンドかどうかを判定
        /// Punished, Eight_Pages が該当（ラウンド終了後にアイテム再装備が必要）
        /// ※Punishedはアイテムが没収されるため再装備が必要
        /// </summary>
        public static bool IsItemReminderRound(ToNRoundType roundType)
        {
            return roundType == ToNRoundType.Punished ||
                   roundType == ToNRoundType.Eight_Pages;
        }

        /// <summary>
        /// オルタネート系ラウンドかどうかを判定
        /// Alternate, Fog_Alternate, Ghost_Alternate が該当
        /// </summary>
        public static bool IsAlternateVariant(ToNRoundType roundType)
        {
            return roundType == ToNRoundType.Alternate ||
                   roundType == ToNRoundType.Fog_Alternate ||
                   roundType == ToNRoundType.Ghost_Alternate;
        }

        /// <summary>
        /// 通常スロットを消費するラウンドかどうかを判定（Normal + Moon）
        /// </summary>
        public static bool ConsumesNormalSlot(ToNRoundType roundType)
        {
            return IsNormalRound(roundType) || IsMoonRound(roundType);
        }

        /// <summary>
        /// 特殊スロットを消費するラウンドかどうかを判定
        /// </summary>
        public static bool ConsumesSpecialSlot(ToNRoundType roundType)
        {
            return IsSpecialRound(roundType);
        }
    }
}
