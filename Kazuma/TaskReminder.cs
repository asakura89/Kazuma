using System.Xml.Serialization;

namespace Kazuma;

public record TaskReminder(
    [property: XmlAttribute("task")] String Task,
    [property: XmlAttribute("dueDate")] DateTime DueDate,
    [property: XmlAttribute("lastRemind")] DateTime LastRemind,
    [property: XmlAttribute("remindAgain")] TimeSpan RemindAgain,
    [property: XmlAttribute("note")] String Note) {
    public TaskReminder() :
        this(String.Empty, DateTime.MinValue, DateTime.MinValue, TimeSpan.Zero, String.Empty) { }
}
