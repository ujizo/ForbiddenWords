
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace ForbiddenWords;

public static class Scanner
{
    public static async Task<ScanResult> RunAsync(
        string source,
        string destination,
        string[] words,
        IProgress<ScanProgress>? progress,
        ManualResetEventSlim pause,
        CancellationToken token)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);

        if (!Directory.Exists(source))
            throw new Exception("Папка поиска не существует.");

        if (source.Equals(destination, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Папки должны отличаться.");

        string[] validWords = words
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length)
            .ToArray();

        if (validWords.Length == 0)
            throw new Exception("Введите запрещённые слова.");

        string pattern = string.Join("|", validWords.Select(Regex.Escape));

        var regex = new Regex(
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        var files = new ConcurrentBag<string>();
        var directories = new Stack<string>();

        directories.Push(source);

        string destinationPrefix =
            destination.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        while (directories.Count > 0)
        {
            token.ThrowIfCancellationRequested();

            string directory = directories.Pop();

            try
            {
                foreach (string file in Directory.GetFiles(directory, "*.txt"))
                {
                    if (!Path.GetFullPath(file).StartsWith(
                        destinationPrefix,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(file);
                    }
                }

                foreach (string dir in Directory.GetDirectories(directory))
                {
                    try
                    {
                        if (Path.GetFullPath(dir).StartsWith(
                            destinationPrefix,
                            StringComparison.OrdinalIgnoreCase))
                            continue;

                        if ((File.GetAttributes(dir) &
                            FileAttributes.ReparsePoint) == 0)
                        {
                            directories.Push(dir);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        string[] allFiles = files.ToArray();

        Directory.CreateDirectory(destination);

        var result = new ScanResult();

        int done = 0;
        int found = 0;

        await Parallel.ForEachAsync(
            allFiles,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = token
            },
            async (file, ct) =>
            {
                pause.Wait(ct);

                try
                {
                    string content = await File.ReadAllTextAsync(file, ct);

                    MatchCollection matches = regex.Matches(content);

                    if (matches.Count > 0)
                    {
                        var counts = new Dictionary<string, int>(
                            StringComparer.OrdinalIgnoreCase
                        );

                        foreach (Match match in matches)
                        {
                            counts.TryGetValue(match.Value, out int count);
                            counts[match.Value] = count + 1;
                        }

                        foreach (var item in counts)
                        {
                            result.WordCounts.AddOrUpdate(
                                item.Key,
                                item.Value,
                                (_, old) => old + item.Value
                            );
                        }

                        string relative = Path.GetRelativePath(source, file);

                        string original = Path.Combine(
                            destination,
                            "Original",
                            relative
                        );

                        string redacted = Path.Combine(
                            destination,
                            "Redacted",
                            relative
                        );

                        Directory.CreateDirectory(
                            Path.GetDirectoryName(original)!
                        );

                        Directory.CreateDirectory(
                            Path.GetDirectoryName(redacted)!
                        );

                        await File.WriteAllTextAsync(
                            original,
                            content,
                            ct
                        );

                        string replaced = regex.Replace(
                            content,
                            "*******"
                        );

                        await File.WriteAllTextAsync(
                            redacted,
                            replaced,
                            ct
                        );

                        result.Files.Add(new FoundFile
                        {
                            OriginalPath = file,
                            CopyPath = original,
                            RedactedPath = redacted,
                            Size = new FileInfo(file).Length,
                            Replacements = matches.Count
                        });

                        Interlocked.Increment(ref found);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                }

                int current = Interlocked.Increment(ref done);

                progress?.Report(new ScanProgress
                {
                    Done = current,
                    Total = allFiles.Length,
                    Found = Volatile.Read(ref found),
                    CurrentFile = file
                });

                await Task.CompletedTask;
            }
        );

        result.Checked = done;

        Report.Create(destination, result);

        return result;
    }
}
