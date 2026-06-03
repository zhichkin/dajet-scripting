# DaJet Script 2.0

Данная версия DaJet Script является продолжением проекта [DaJet](https://zhichkin.github.io/). В новой версии планируется выполнить рефакторинг существующего кода, сохранив при этом синтаксис и все базовые концепции. Функционал предыдущей версии постепенно переносится в этот репозиторий.

На данный момент DaJet Script 2.0 уже используется, и вполне успешно, в новых проектах [DaJet MCP Server](https://github.com/zhichkin/dajet-mcp-server) и [DaJet Http Server](https://github.com/zhichkin/dajet-http-server).

В текущей версии поддерживаются следующие команды:
- DECLARE
- USE
- SELECT
- PRINT
- RETURN

Поддерживается работа с базами данных SQL Server и PostgreSQL.

Для выполнения скриптов необходимо подключить пакет NuGet DaJet.Interpreter.

```
> dotnet add package DaJet.Interpreter --version 1.0.3
```

Этот пакет имеет следующие зависимости:
- DaJet.Interpreter
  - DaJet.Scripting
    - DaJet.Metadata
      - DaJet.FileLogger
      - DaJet.TypeSystem
      - DaJet.Data
        - Npgsql
        - Microsoft.Data.SqlClient

### Пример использования интерпретатора DaJet Script 2.0

**Файл скрипта DaJet Script ```test.djs```**

```SQL
DECLARE @КодТовара string -- Входящий параметр скрипта
DECLARE @Результат array  -- Результат выполнения скрипта

USE 'MS_TEST'
  SELECT Ссылка, Код, Наименование
    INTO @Результат
    FROM Справочник.Номенклатура
   WHERE Код = @КодТовара
END

RETURN @Результат
```

**Код программы на C#**

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = true,
    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
};

static void Main()
{
  JsonOptions.Converters.Add(new DictionaryJsonConverter());

  string MS_TEST = "Data Source=server;Initial Catalog=database;Integrated Security=True;Encrypt=False;";

  MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);

  string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", "test.djs");

  using (StreamReader reader = new(filePath, Encoding.UTF8))
  {
    source = reader.ReadToEnd();
  }

  Interpreter interpreter = new(in source);

  Dictionary<string, object> parameters = new()
  {
    { "КодТовара", "00000002" }
  };

  object value = interpreter.Execute(in parameters);

  string json = JsonSerializer.Serialize(value, value.GetType(), JsonOptions);

  Console.WriteLine(json);
}
```
