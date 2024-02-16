파일을 뒤에서부터 거꾸로 읽으며, 특정 문자를 만날 때까지 읽습니다.

파일을 읽은 만큼 Truncate할 수 있습니다.

```C#
static void Main(string[] args)
        {
            int cnt = 1;
            using (var reader = new ReverseTextReader(@"D:\TEST.txt", 4096, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReverseRead("\r\n")) != null) // 줄바꿈을 만날 때까지 거꾸로 읽음.
                {
                    Console.WriteLine($"{cnt++} str: {line}");
                    Thread.Sleep(1000);
                    reader.FileTruncateReadStr();
                }
            }

            Console.ReadKey();
        }
```
