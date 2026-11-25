
DECLARE @result object

DECLARE @union_default  union
DECLARE @union_boolean  union = TRUE
DECLARE @union_decimal  union = 12.34
DECLARE @union_integer  union = 12345
DECLARE @union_datetime union = '2026-01-01 12:34:56'
DECLARE @union_string   union = 'Это строка'
DECLARE @union_entity   union = {333:643c6b9d-cacf-4048-11f1-3ce54d7b5bf7}

-- [ERROR] DECLARE @union union = ['a','b','c']
-- [ERROR] DECLARE @array array(union) = ['a','b','c']

SET @result.default  = @union_default
SET @result.boolean  = @union_boolean
SET @result.decimal  = @union_decimal
SET @result.integer  = @union_integer
SET @result.datetime = @union_datetime
SET @result.string   = @union_string
SET @result.entity   = @union_entity

RETURN @result