using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ToNStatTool.Helpers
{
    /// <summary>
    /// コントロール検索ユーティリティ
    /// </summary>
    public static class ControlFinder
    {
        /// <summary>
        /// 指定された名前のコントロールを再帰的に検索
        /// </summary>
        public static Control FindControlRecursive(Control parent, string name)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Name == name)
                    return control;
                var found = FindControlRecursive(control, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }

    /// <summary>
    /// JSON関連ユーティリティ
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// JSONを整形して表示
        /// </summary>
        public static string FormatJson(string json)
        {
            try
            {
                var jsonObject = JObject.Parse(json);
                return jsonObject.ToString(Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }
    }
}
