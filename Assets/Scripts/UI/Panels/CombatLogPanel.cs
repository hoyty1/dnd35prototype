using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dedicated panel for combat log message history, formatting, and scroll behavior.
/// Extracted from CombatUI to reduce responsibilities in the main combat UI class.
/// </summary>
public class CombatLogPanel : MonoBehaviour
{
    private CombatUI _combatUI;

    // Backward-compatible UI references (legacy Text + scrollable content path)
    private Text _legacyCombatLogText;
    private GameObject _combatLogContent;
    private ScrollRect _combatLogScrollRect;

    // Message history
    private readonly List<string> _messageHistory = new List<string>();
    private const int MaxMessages = 500;

    // Formatting/cache
    private readonly StringBuilder _builder = new StringBuilder();
    private bool _autoScroll = true;

    public void Initialize(CombatUI combatUI, Text legacyCombatLogText, GameObject combatLogContent, ScrollRect combatLogScrollRect)
    {
        _combatUI = combatUI;
        _legacyCombatLogText = legacyCombatLogText;
        _combatLogContent = combatLogContent;
        _combatLogScrollRect = combatLogScrollRect;
    }

    /// <summary>
    /// Core entry point for adding a message to the combat log.
    /// </summary>
    public void AddMessage(string message, MessageType type = MessageType.Normal)
    {
        if (string.IsNullOrEmpty(message))
            return;

        string formatted = FormatMessageByType(message, type);
        formatted = HighlightCombatKeywords(formatted);

        _messageHistory.Add(formatted);

        if (_messageHistory.Count > MaxMessages)
            _messageHistory.RemoveAt(0);

        if (_combatLogContent != null)
        {
            AppendScrollableLogMessage(formatted);
            TrimScrollableLogChildren();

            if (_autoScroll)
                StartCoroutine(ScrollToBottomNextFrame());

            return;
        }

        // Legacy fallback (single text field)
        if (_legacyCombatLogText != null)
        {
            _legacyCombatLogText.supportRichText = true;
            _legacyCombatLogText.text = formatted;
        }
    }

    public void AddMessages(IEnumerable<string> messages, MessageType type = MessageType.Normal)
    {
        if (messages == null)
            return;

        foreach (string message in messages)
            AddMessage(message, type);
    }

    public void AddTurnSeparator(int turnNumber)
    {
        AddMessage($"<color=#888888>─────── Turn {turnNumber} ───────</color>");
    }

    public void ClearLog()
    {
        _messageHistory.Clear();
        _builder.Clear();

        if (_combatLogContent != null)
        {
            foreach (Transform child in _combatLogContent.transform)
                Destroy(child.gameObject);
        }

        if (_legacyCombatLogText != null)
            _legacyCombatLogText.text = string.Empty;
    }

    public void SetAutoScroll(bool enabled)
    {
        _autoScroll = enabled;
    }

    public void ScrollToBottom()
    {
        if (_combatLogScrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        _combatLogScrollRect.verticalNormalizedPosition = 0f;
    }

    public void ScrollToTop()
    {
        if (_combatLogScrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        _combatLogScrollRect.verticalNormalizedPosition = 1f;
    }

    public int GetMessageCount()
    {
        return _messageHistory.Count;
    }

    public List<string> GetAllMessages()
    {
        return new List<string>(_messageHistory);
    }

    public string ExportLog()
    {
        _builder.Clear();
        _builder.AppendLine("=== COMBAT LOG EXPORT ===");
        _builder.AppendLine($"Messages: {_messageHistory.Count}");
        _builder.AppendLine($"Timestamp: {System.DateTime.Now}");
        _builder.AppendLine();

        for (int i = 0; i < _messageHistory.Count; i++)
        {
            string cleanMessage = Regex.Replace(_messageHistory[i], "<.*?>", "");
            _builder.AppendLine(cleanMessage);
        }

        return _builder.ToString();
    }

    public void LogAttack(string attackerName, string targetName, int roll, int modifier, int total, int targetAC, bool hit)
    {
        string message = $"{attackerName} attacks {targetName}: Roll {roll} + {modifier} = {total} vs AC {targetAC}";
        AddMessage(message, hit ? MessageType.Hit : MessageType.Miss);
    }

    public void LogDamage(string targetName, int damage, string damageType = "")
    {
        string typeText = string.IsNullOrEmpty(damageType) ? "" : $" {damageType}";
        string message = $"{targetName} takes {damage}{typeText} damage";
        AddMessage(message, MessageType.Damage);
    }

    public void LogCritical(string attackerName, string targetName)
    {
        string message = $"CRITICAL HIT! {attackerName} critically strikes {targetName}!";
        AddMessage(message, MessageType.Critical);
    }

    public void LogSavingThrow(string characterName, string saveType, int roll, int modifier, int total, int dc, bool success)
    {
        string message = $"{characterName} {saveType} save: Roll {roll} + {modifier} = {total} vs DC {dc}";
        AddMessage(message, success ? MessageType.Success : MessageType.Miss);
    }

    public void LogSkillCheck(string characterName, string skillName, int roll, int modifier, int total, int dc, bool success)
    {
        string message = $"{characterName} {skillName} check: Roll {roll} + {modifier} = {total} vs DC {dc}";
        AddMessage(message, success ? MessageType.Success : MessageType.Miss);
    }

    public void LogManeuver(string attackerName, string maneuverName, string targetName, bool success)
    {
        string message = $"{attackerName} attempts {maneuverName} on {targetName}";
        AddMessage(message, success ? MessageType.Success : MessageType.Miss);
    }

    public void LogTurnStart(string characterName, int round)
    {
        AddMessage($"\n--- Round {round}: {characterName}'s Turn ---", MessageType.System);
    }

    public void LogTurnEnd(string characterName)
    {
        AddMessage($"{characterName} ends their turn", MessageType.System);
    }

    public void LogDeath(string characterName)
    {
        AddMessage($"💀 {characterName} has been defeated!", MessageType.Error);
    }

    public void LogCombatEnd(string result)
    {
        AddMessage($"\n=== Combat Ended: {result} ===", MessageType.System);
    }

    private void AppendScrollableLogMessage(string formatted)
    {
        GameObject msgObj = new GameObject($"LogMsg_{_messageHistory.Count}");
        msgObj.transform.SetParent(_combatLogContent.transform, false);

        Text text = msgObj.AddComponent<Text>();
        text.supportRichText = true;
        text.text = formatted;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (text.font == null)
            text.font = Font.CreateDynamicFontFromOSFont("Arial", 11);

        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        LayoutElement layout = msgObj.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1;
    }

    private void TrimScrollableLogChildren()
    {
        if (_combatLogContent == null)
            return;

        while (_combatLogContent.transform.childCount > MaxMessages)
        {
            Transform oldest = _combatLogContent.transform.GetChild(0);
            Destroy(oldest.gameObject);
        }
    }

    private string FormatMessageByType(string message, MessageType type)
    {
        string prefix = GetMessagePrefix(type);
        string color = GetMessageColor(type);

        if (!string.IsNullOrEmpty(color))
            return $"<color={color}>{prefix}{message}</color>";

        return prefix + message;
    }

    private string GetMessagePrefix(MessageType type)
    {
        switch (type)
        {
            case MessageType.System:
                return "► ";
            case MessageType.Error:
                return "✖ ";
            case MessageType.Success:
                return "✓ ";
            case MessageType.Warning:
                return "⚠ ";
            case MessageType.Critical:
                return "★ ";
            default:
                return string.Empty;
        }
    }

    private string GetMessageColor(MessageType type)
    {
        switch (type)
        {
            case MessageType.Hit:
                return "#4CAF50";
            case MessageType.Miss:
                return "#9E9E9E";
            case MessageType.Critical:
                return "#FFD700";
            case MessageType.Damage:
                return "#F44336";
            case MessageType.Heal:
                return "#00BCD4";
            case MessageType.System:
                return "#2196F3";
            case MessageType.Error:
                return "#FF5722";
            case MessageType.Success:
                return "#8BC34A";
            case MessageType.Warning:
                return "#FF9800";
            default:
                return null;
        }
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;

        if (_combatLogScrollRect == null)
            yield break;

        Canvas.ForceUpdateCanvases();
        _combatLogScrollRect.verticalNormalizedPosition = 0f;
    }

    private string HighlightCombatKeywords(string text)
    {
        text = text.Replace("- HIT!", "- <color=#66FF66><b>HIT!</b></color>");
        text = text.Replace("- MISS!", "- <color=#FF4444><b>MISS!</b></color>");
        text = text.Replace("CRITICAL HIT!", "<color=#FFD700><b>CRITICAL HIT!</b></color>");
        text = text.Replace("CRIT!", "<color=#FFD700><b>CRIT!</b></color>");
        text = text.Replace("*** Critical Threat", "<color=#FFA500><b>*** Critical Threat</b></color>");
        text = text.Replace("CONFIRMED!", "<color=#FFD700><b>CONFIRMED!</b></color>");
        text = text.Replace("Not confirmed", "<color=#AAAAAA>Not confirmed</color>");
        text = text.Replace("(NATURAL 20!)", "<color=#FFD700><b>(NATURAL 20!)</b></color>");
        text = text.Replace("(NAT 20!)", "<color=#FFD700><b>(NAT 20!)</b></color>");
        text = text.Replace("(NATURAL 1!)", "<color=#FF4444><b>(NATURAL 1!)</b></color>");
        text = text.Replace("(NAT 1!)", "<color=#FF4444><b>(NAT 1!)</b></color>");
        text = text.Replace("has been slain!", "<color=#FF6666><b>has been slain!</b></color>");
        text = text.Replace("no penalty", "<color=#66FF66>no penalty</color>");
        text = text.Replace("beyond maximum range!", "<color=#FF4444><b>beyond maximum range!</b></color>");
        text = text.Replace("Power Attack", "<color=#FF9933>Power Attack</color>");
        text = text.Replace("Rapid Shot", "<color=#66CCFF>Rapid Shot</color>");
        text = text.Replace("Point Blank Shot", "<color=#66FF66>Point Blank Shot</color>");
        text = text.Replace("Fighting Defensively", "<color=#99CCFF>Fighting Defensively</color>");
        text = text.Replace("Shooting into melee", "<color=#FFCC66>Shooting into melee</color>");
        text = text.Replace("Precise Shot", "<color=#99FF99>Precise Shot</color>");

        text = text.Replace("SPELL CAST!", "<color=#BB88FF><b>SPELL CAST!</b></color>");
        text = text.Replace("Metamagic:", "<color=#FFB833><b>Metamagic:</b></color>");
        text = text.Replace("Empower:", "<color=#FFB833>Empower:</color>");
        text = text.Replace("QUICKENED", "<color=#FFD700><b>QUICKENED</b></color>");
        text = text.Replace("⚡", "<color=#FFB833>⚡</color>");
        text = text.Replace("healed!", "<color=#66FF66><b>healed!</b></color>");
        text = text.Replace("BUFF APPLIED!", "<color=#6699FF><b>BUFF APPLIED!</b></color>");
        text = text.Replace("RESISTED!", "<color=#AAAAAA><b>RESISTED!</b></color>");
        text = text.Replace("Touch Attack", "<color=#BB88FF>Touch Attack</color>");
        text = text.Replace("Fortitude save", "<color=#FFAA44>Fortitude save</color>");
        text = text.Replace("Reflex save", "<color=#FFAA44>Reflex save</color>");
        text = text.Replace("Will save", "<color=#FFAA44>Will save</color>");

        text = text.Replace("═══════════════════════════════════", "<color=#888888>═══════════════════════════════════</color>");
        text = text.Replace("total damage", "<color=#FFAA44><b>total damage</b></color>");
        text = text.Replace(" damage", " <color=#FFAA44>damage</color>");
        text = text.Replace(" → ", " <color=#FFFF66>→</color> ");
        return text;
    }
}

/// <summary>
/// Combat log message styling categories.
/// </summary>
public enum MessageType
{
    Normal,
    Hit,
    Miss,
    Critical,
    Damage,
    Heal,
    System,
    Error,
    Success,
    Warning
}
