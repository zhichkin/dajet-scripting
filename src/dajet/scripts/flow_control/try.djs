
DECLARE @test string = 'test'

WHILE @test = 'test'

TRY
  SET @test = 'try' --THROW 'throw'
CATCH
  SET @test = 'catch' --THROW 'throw'
FINALLY
  SET @test = 'finally' --THROW 'throw' --CONTINUE --BREAK --RETURN 'finally'
END

END

RETURN @test