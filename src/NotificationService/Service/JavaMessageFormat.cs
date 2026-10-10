using System.Text;

namespace PiggyMetrics.NotificationService.Service;

public static class JavaMessageFormat
{
    public static string Format(string pattern, string argument)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '\'')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                {
                    builder.Append('\'');
                    i++;
                    continue;
                }

                i++;
                while (i < pattern.Length)
                {
                    if (pattern[i] == '\'')
                    {
                        if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                        {
                            builder.Append('\'');
                            i += 2;
                            continue;
                        }

                        break;
                    }

                    builder.Append(pattern[i]);
                    i++;
                }

                continue;
            }

            if (pattern[i] == '{' && i + 2 < pattern.Length && pattern[i + 1] == '0' && pattern[i + 2] == '}')
            {
                builder.Append(argument);
                i += 2;
                continue;
            }

            builder.Append(pattern[i]);
        }

        return builder.ToString();
    }
}
