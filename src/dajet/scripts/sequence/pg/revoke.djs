
USE 'PG_TEST'

  REVOKE SEQUENCE so_import ON РегистрСведений.ИсходящаяОчередь

END

RETURN 'Sequence [so_import] revoked successfully'