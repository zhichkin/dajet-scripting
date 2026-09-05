# DaJet Script 2.0 <a href="https://www.nuget.org/packages/DaJet.Scripting"><img width="32" height="32" alt="DaJet Metadata at NuGet" src="https://github.com/user-attachments/assets/579f736b-975b-4657-a813-f26329afbcfb"/></a>

Данная версия DaJet Script является продолжением проекта [DaJet](https://zhichkin.github.io/). В новой версии планируется выполнить рефакторинг существующего кода, сохранив при этом синтаксис и все базовые концепции. Функционал предыдущей версии постепенно переносится в этот репозиторий. Одной из причин является необходимость перевода проекта DaJet на новую версию библиотеки [DaJet.Metadata](https://github.com/zhichkin/dajet-metadata), которая была перписана с нуля.

На данный момент DaJet Script 2.0 уже используется, и вполне успешно, в новых проектах [DaJet MCP Server](https://github.com/zhichkin/dajet-mcp-server) и [DaJet Http Server](https://github.com/zhichkin/dajet-http-server), а также [DaJet Studio](https://github.com/zhichkin/dajet-studio).

В текущей версии поддерживаются следующие команды:
- DECLARE или PRIVATE (объявление переменных)
- SET (присваивание значений переменным)
- USE (открытие соединения с базой данных)
- SELECT (с поддержкой общих табличных выражений)
- INSERT (вставка одной записи)
- CONSUME (деструктивное чтение из базы данных)
- PRINT
- RETURN (возрат значений из скрипта по WEB API)
- IF, FOR, WHILE, BREAK, CONTINUE (условия и циклы)
- TRY, THROW (обработка и вызов исключений)
- SLEEP (приостановка потока выполнения)
- JSON (функция сериализации)

Поддерживается работа с базами данных SQL Server и PostgreSQL.

Для выполнения скриптов необходимо подключить в свой проект NuGet-пакет **DaJet.Interpreter**.

```
> dotnet add package DaJet.Interpreter --version 1.0.20
```

Рекомендуется подключение NuGet-пакета **DaJet.Host** с поддержкой функций хостинга скриптов:
- обслуживание скриптов из каталога публикации, включая вложенные каталоги;
- режим хоста "только чтение" (разрешает выполнять только SELECT);
- кэширование скриптов при первом к ним обращении;
- автоматическая загрузка скриптов при старте хоста;
- автоматическое обновление кэша скриптов при их добавлении/изменении без перезагрузки хоста;
- получение статуса долго выполняемых скриптов;
- программная отмена выполнения долгих скриптов;
- контроль выполнения единственного экземпляра скрипта (singleton).

```
> dotnet add package DaJet.Host --version 1.0.13
```

Этот пакет имеет следующие зависимости:
- DaJet.Host (текущий репозиторий)
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

  Script script = new ScriptBuilder().FromFile(in filePath).Build();

  Interpreter interpreter = new(in script);

  Dictionary<string, object> parameters = new()
  {
    { "КодТовара", "00000002" }
  };

  object value = interpreter.Execute(in parameters);

  string json = JsonSerializer.Serialize(value, value.GetType(), JsonOptions);

  Console.WriteLine(json);
}
```

### Пример использования хоста DaJet Script 2.0

Предполагается, что в корневом каталоге установки хоста DaJet имеется каталог публикации скриптов ```scripts```, в котором уже опубликован скрипт ```long-task-test.djs```. Ниже следующий скрипт демонстрирует в том числе использование специальных директив выполнения скрипта. Скрипт запускается при старте хоста DaJet, является долгим (выделяется отдельный поток операционной системы), а также в каждый момент времени выполняется единственный экземпляр этого скрипта.

**Код скрипта DaJet ```long-task-test.djs```**

```SQL
# STARTUP                    -- Запуск скрипта при старте хоста DaJet
# LONG_TASK                  -- Долгий скрипт (отдельный поток выполнения)
# SINGLETON 'LONG TASK TEST' -- Директива контроля единственного выполнения

PRIVATE @Счётчик integer

PRINT '[LONG TASK TEST] START'

WHILE @Счётчик < 10

  SET @Счётчик = @Счётчик + 1

  IF @Счётчик = 5 THEN BREAK END

  PRINT 'LOOP: ' + @Счётчик

  SLEEP 3

END

PRINT '[LONG TASK TEST] END'
```

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = true,
    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
};

private static DaJetHost _host;
private static System.Timers.Timer _heartbeat;

static void Main()
{
  JsonOptions.Converters.Add(new EntityJsonConverter());
  JsonOptions.Converters.Add(new DataTypeJsonConverter());
  JsonOptions.Converters.Add(new DataObjectJsonConverter());
  JsonOptions.Converters.Add(new JsonStringEnumConverter());

  string MS_TEST = "Data Source=server;Initial Catalog=database;Integrated Security=True;Encrypt=False;";

  MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);

  _host = DaJetHost.Create("scripts").Run();

  Heartbeat(); // Периодически мониторим состояние долгих скриптов

  _ = host.RunAsync("long-task-test.djs").ContinueWith(ShowAsyncResult);

  Console.WriteLine("Press any key to exit the program ...");

  _ = Console.ReadKey(true);
}

private static void ShowAsyncResult(Task<object> task)
{
    if (task.IsCompletedSuccessfully)
    {
        object value = task.Result;

        if (value is not null)
        {
            string json = JsonSerializer.Serialize(value, value.GetType(), JsonOptions);

            Console.WriteLine($"Task [{task.Id}] return value:");
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"Task [{task.Id}] returned null value.");
        }
    }
    else if (task.IsCanceled)
    {
        Console.WriteLine($"Task [{task.Id}] is canceled.");
    }
    else
    {
        Exception error = task.Exception.Flatten().InnerException;

        Console.WriteLine($"Task [{task.Id}] is faulted: {error.Message}");
    }
}

private static void Heartbeat()
{
    System.Timers.Timer timer = new();

    if (Interlocked.CompareExchange(ref _heartbeat, timer, null) is not null)
    {
        timer.Dispose();
    }
    else
    {
        _heartbeat.AutoReset = true;
        _heartbeat.Elapsed += ShowRunningTasks;
        _heartbeat.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
    }

    _heartbeat.Start();
}

private static void ShowRunningTasks(object sender, ElapsedEventArgs args)
{
    foreach (RunningTaskStatus status in _host.GetRunningTasks())
    {
        Console.WriteLine(status.ToString());
    }
}
```

**Результат выполнения программы (консольный вывод)**

```text
[LONG TASK TEST] START
LOOP: 1
Press any key to exit the program ...
Task [32] is faulted: Duplicate singleton run: [long-task-test.djs] {LONG TASK TEST}
[28] {Running} long-task-test.djs "LONG TASK TEST"
LOOP: 2
[28] {Running} long-task-test.djs "LONG TASK TEST"
[28] {Running} long-task-test.djs "LONG TASK TEST"
[28] {Running} long-task-test.djs "LONG TASK TEST"
LOOP: 3
[28] {Running} long-task-test.djs "LONG TASK TEST"
[28] {Running} long-task-test.djs "LONG TASK TEST"
[28] {Running} long-task-test.djs "LONG TASK TEST"
LOOP: 4
[28] {Running} long-task-test.djs "LONG TASK TEST"
[28] {Running} long-task-test.djs "LONG TASK TEST"
[28] {Running} long-task-test.djs "LONG TASK TEST"
[LONG TASK TEST] END
```
