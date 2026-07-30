
USE 'PG_TEST'

  APPLY SEQUENCE so_import ON РегистрСведений.ИсходящаяОчередь(НомерСообщения) RECALCULATE

END

RETURN 'Sequence [so_import] applied successfully'