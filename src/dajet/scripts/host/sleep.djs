
-- Test script cancellation

DECLARE @input string

PRINT 'SLEEP ' + @input

SLEEP 10

RETURN '[SLEEP]'