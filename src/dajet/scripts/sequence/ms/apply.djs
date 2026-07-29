
USE 'MS_TEST'

  APPLY SEQUENCE so_import ON РегистрСведений.ИсходящаяОчередь(НомерСообщения)

END

RETURN 'Sequence [so_import] applied successfully'