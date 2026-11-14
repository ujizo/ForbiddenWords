
using System.Text;

namespace ForbiddenWords;

public static class Report
{
    public static void Create(string destination, ScanResult result)
    {
        StringBuilder text = new();

        text.AppendLine("ОТЧЕТ О ПОИСКЕ ЗАПРЕЩЕННЫХ СЛОВ");
        text.AppendLine("Дата: " + DateTime.Now);
        text.AppendLine("Проверено файлов: " + result.Checked);
        text.AppendLine("Найдено файлов: " + result.Files.Count);
        text.AppendLine();

        text.AppendLine("НАЙДЕННЫЕ ФАЙЛЫ");

        foreach (FoundFile file in result.Files.OrderBy(x => x.OriginalPath))
        {
            text.AppendLine("-------------------------");
            text.AppendLine("Исходный путь: " + file.OriginalPath);
            text.AppendLine("Копия: " + file.CopyPath);
            text.AppendLine("Обработанная копия: " + file.RedactedPath);
            text.AppendLine("Размер: " + file.Size + " байт");
            text.AppendLine("Количество замен: " + file.Replacements);
        }

        text.AppendLine();
        text.AppendLine("ТОП-10 ЗАПРЕЩЕННЫХ СЛОВ");

        foreach (var word in result.WordCounts
            .OrderByDescending(x => x.Value)
            .Take(10))
        {
            text.AppendLine($"{word.Key} - {word.Value}");
        }

        Directory.CreateDirectory(destination);

        File.WriteAllText(
            Path.Combine(destination, "Report.txt"),
            text.ToString(),
            Encoding.UTF8
        );
    }
}
