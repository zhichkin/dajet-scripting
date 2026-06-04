# DaJet Script 2.0

Данная версия DaJet Script является продолжением проекта [DaJet](https://zhichkin.github.io/). В новой версии планируется выполнить рефакторинг существующего кода, сохранив при этом синтаксис и все базовые концепции. Функционал предыдущей версии постепенно переносится в этот репозиторий. Одной из причин является необходимость перевода проекта DaJet на новую версию библиотеки [DaJet.Metadata](https://github.com/zhichkin/dajet-metadata), которая была перписана с нуля.

На данный момент DaJet Script 2.0 уже используется, и вполне успешно, в новых проектах [DaJet MCP Server](https://github.com/zhichkin/dajet-mcp-server) и [DaJet Http Server](https://github.com/zhichkin/dajet-http-server), а также [DaJet Studio](https://github.com/zhichkin/dajet-studio).

В текущей версии поддерживаются следующие команды:
- DECLARE | PRIVATE
- USE
- SELECT
- PRINT
- RETURN
- JSON (функция сериализации)

Поддерживается работа с базами данных SQL Server и PostgreSQL.

Для выполнения скриптов необходимо подключить в свой проект NuGet-пакет **DaJet.Interpreter**.

```
> dotnet add package DaJet.Interpreter --version 1.0.3
```

Этот пакет имеет следующие зависимости:
- DaJet.Interpreter (текущий репозиторий)
  - DaJet.Scripting (текущий репозиторий)
    - [DaJet.Metadata](https://github.com/zhichkin/dajet-metadata)
      - DaJet.FileLogger
      - DaJet.TypeSystem
      - DaJet.Data
        - Npgsql
        - Microsoft.Data.SqlClient

### Пример использования интерпретатора DaJet Script 2.0

**Файл скрипта DaJet Script ```test.djs```**

```SQL
DECLARE @КодТовара string -- Входящий параметр скрипта
PRIVATE @Результат array  -- Результат выполнения скрипта

USE 'MS_TEST'
  SELECT Ссылка, Код, Наименование
    INTO @Результат
    FROM Справочник.Номенклатура
   WHERE Код = @КодТовара
END

RETURN @Результат -- или JSON(@Результат)
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

  string source;

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
