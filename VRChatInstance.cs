using System;
using System.Collections.Generic;
using System.Drawing;

namespace ToNStatTool
{
	/// <summary>
	/// VRChatのインスタンス種別
	/// </summary>
	public enum VRChatInstanceType
	{
		Unknown,
		Public,
		FriendsPlus,
		Friends,
		InvitePlus,
		Invite,
		GroupPublic,
		GroupPlus,
		Group
	}

	/// <summary>
	/// インスタンスIDを解析した結果。
	/// 書式はVRChat公式APIドキュメント（vrchat.community / Instances）の対応表に準拠する:
	///   Public       : 修飾なし
	///   Friends+     : ~hidden(usr_...)
	///   Friends      : ~friends(usr_...)
	///   Invite+      : ~private(usr_...)~canRequestInvite
	///   Invite       : ~private(usr_...)
	///   Group Public : ~group(grp_...)~groupAccessType(public)
	///   Group+       : ~group(grp_...)~groupAccessType(plus)
	///   Group        : ~group(grp_...)~groupAccessType(members)
	/// </summary>
	public class VRChatInstanceInfo
	{
		/// <summary>解析元の文字列</summary>
		public string RawId { get; set; } = "";

		/// <summary>ワールドID（wrld_...）</summary>
		public string WorldId { get; set; } = "";

		/// <summary>インスタンス名（先頭の数字部分）</summary>
		public string InstanceName { get; set; } = "";

		/// <summary>インスタンス種別</summary>
		public VRChatInstanceType Type { get; set; } = VRChatInstanceType.Unknown;

		/// <summary>インスタンス所有者のユーザーID（private/friends/hiddenの場合）</summary>
		public string OwnerId { get; set; } = "";

		/// <summary>グループID（グループインスタンスの場合）</summary>
		public string GroupId { get; set; } = "";

		/// <summary>リージョンコード（us/use/usw/eu/jp など。未指定の場合は空）</summary>
		public string Region { get; set; } = "";

		/// <summary>18+インスタンスかどうか</summary>
		public bool IsAgeGated { get; set; } = false;

		/// <summary>解析に成功したか（ワールドIDが取れたか）</summary>
		public bool IsValid => !string.IsNullOrEmpty(WorldId);

		/// <summary>
		/// 種別の表示名（VRChatクライアントの表記に合わせる）
		/// </summary>
		public string TypeDisplayName
		{
			get
			{
				switch (Type)
				{
					case VRChatInstanceType.Public: return "Public";
					case VRChatInstanceType.FriendsPlus: return "Friends+";
					case VRChatInstanceType.Friends: return "Friends";
					case VRChatInstanceType.InvitePlus: return "Invite+";
					case VRChatInstanceType.Invite: return "Invite";
					case VRChatInstanceType.GroupPublic: return "Group Public";
					case VRChatInstanceType.GroupPlus: return "Group+";
					case VRChatInstanceType.Group: return "Group";
					default: return "不明";
				}
			}
		}

		/// <summary>
		/// リージョンの表示名（大文字。未指定は "US"（VRChat既定））
		/// </summary>
		public string RegionDisplayName
		{
			get
			{
				if (string.IsNullOrEmpty(Region)) return "US";
				return Region.ToUpperInvariant();
			}
		}

		/// <summary>
		/// 「Public / JP」のような1行表示
		/// </summary>
		public string ShortDescription
		{
			get
			{
				if (!IsValid) return "-";

				string text = $"{TypeDisplayName} / {RegionDisplayName}";
				if (IsAgeGated) text += " / 18+";
				return text;
			}
		}

		/// <summary>
		/// 種別ごとの表示色（テーマの明暗で読みやすい方を返す）
		/// </summary>
		public Color GetTypeColor(bool isDark)
		{
			switch (Type)
			{
				case VRChatInstanceType.Public:
					return isDark ? Color.FromArgb(102, 204, 255) : Color.FromArgb(0, 102, 153);
				case VRChatInstanceType.FriendsPlus:
					return isDark ? Color.FromArgb(255, 190, 120) : Color.FromArgb(180, 100, 0);
				case VRChatInstanceType.Friends:
					return isDark ? Color.FromArgb(150, 230, 150) : Color.FromArgb(0, 128, 0);
				case VRChatInstanceType.InvitePlus:
				case VRChatInstanceType.Invite:
					return isDark ? Color.FromArgb(230, 150, 230) : Color.FromArgb(128, 0, 128);
				case VRChatInstanceType.GroupPublic:
				case VRChatInstanceType.GroupPlus:
				case VRChatInstanceType.Group:
					return isDark ? Color.FromArgb(255, 220, 120) : Color.FromArgb(160, 120, 0);
				default:
					return isDark ? Color.Silver : Color.DimGray;
			}
		}

		public override string ToString() => ShortDescription;
	}

	/// <summary>
	/// インスタンスIDの解析
	/// </summary>
	public static class VRChatInstanceParser
	{
		/// <summary>
		/// インスタンスID文字列を解析する。
		/// 例: "wrld_xxxx:12345~group(grp_xxx)~groupAccessType(plus)~region(jp)~nonce(...)"
		/// </summary>
		public static VRChatInstanceInfo Parse(string instanceId)
		{
			var info = new VRChatInstanceInfo { RawId = instanceId ?? "" };

			if (string.IsNullOrWhiteSpace(instanceId)) return info;

			// "wrld_xxx:instanceName~..." を分割（ワールドIDに ':' は含まれない）
			string worldPart;
			string instancePart;

			int colonIndex = instanceId.IndexOf(':');
			if (colonIndex >= 0)
			{
				worldPart = instanceId.Substring(0, colonIndex);
				instancePart = instanceId.Substring(colonIndex + 1);
			}
			else
			{
				// ワールドIDが無い（インスタンス部分だけ渡された）場合も解析は続ける
				worldPart = "";
				instancePart = instanceId;
			}

			info.WorldId = worldPart;

			string[] segments = instancePart.Split('~');
			if (segments.Length > 0)
			{
				info.InstanceName = segments[0];
			}

			var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			for (int i = 1; i < segments.Length; i++)
			{
				string segment = segments[i];
				if (string.IsNullOrEmpty(segment)) continue;

				int open = segment.IndexOf('(');
				if (open > 0 && segment.EndsWith(")"))
				{
					string key = segment.Substring(0, open);
					string value = segment.Substring(open + 1, segment.Length - open - 2);
					values[key] = value;
					flags.Add(key);
				}
				else
				{
					// canRequestInvite のように値を持たない修飾子
					flags.Add(segment);
				}
			}

			if (values.TryGetValue("region", out string region))
			{
				info.Region = region;
			}

			info.IsAgeGated = flags.Contains("ageGate");

			// 判定順序を間違えると別種別に化けるため、限定の強い順に見る
			if (flags.Contains("group"))
			{
				values.TryGetValue("group", out string groupId);
				info.GroupId = groupId ?? "";

				values.TryGetValue("groupAccessType", out string accessType);
				switch ((accessType ?? "").ToLowerInvariant())
				{
					case "public":
						info.Type = VRChatInstanceType.GroupPublic;
						break;
					case "plus":
						info.Type = VRChatInstanceType.GroupPlus;
						break;
					case "members":
					default:
						// groupAccessTypeが無いグループインスタンスはメンバー限定扱い
						info.Type = VRChatInstanceType.Group;
						break;
				}
			}
			else if (flags.Contains("private"))
			{
				values.TryGetValue("private", out string ownerId);
				info.OwnerId = ownerId ?? "";
				info.Type = flags.Contains("canRequestInvite")
					? VRChatInstanceType.InvitePlus
					: VRChatInstanceType.Invite;
			}
			else if (flags.Contains("friends"))
			{
				values.TryGetValue("friends", out string ownerId);
				info.OwnerId = ownerId ?? "";
				info.Type = VRChatInstanceType.Friends;
			}
			else if (flags.Contains("hidden"))
			{
				values.TryGetValue("hidden", out string ownerId);
				info.OwnerId = ownerId ?? "";
				info.Type = VRChatInstanceType.FriendsPlus;
			}
			else
			{
				// 種別修飾子が何も無ければPublic
				info.Type = VRChatInstanceType.Public;
			}

			return info;
		}
	}
}
