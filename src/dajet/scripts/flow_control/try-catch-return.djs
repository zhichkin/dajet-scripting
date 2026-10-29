
PRIVATE @test string = 'test'

TRY THROW 'boom'
CATCH RETURN 'catch'
--FINALLY SET @test = 'finally'
END

RETURN @test