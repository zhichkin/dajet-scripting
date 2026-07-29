
PRIVATE @vector decimal

USE 'PG_TEST'

  SELECT VECTOR('so_import') INTO @vector

END

RETURN @vector