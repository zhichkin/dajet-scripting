
DECLARE @Отправитель   string = 'DaJet'
DECLARE @ТипСообщения  string = 'test'
DECLARE @ТелоСообщения string = '{ "test": "тест" }'

USE 'MS_TEST'

   INSERT РегистрСведений.ВходящаяОчередь
   SELECT НомерСообщения = VECTOR('so_import')
        , Отправитель    = 'DaJet'
        , ТипСообщения   = @ТипСообщения
        , ТелоСообщения  = @ТелоСообщения

END