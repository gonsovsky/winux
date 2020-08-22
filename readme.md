Winux - .netCore 2.0 приложение для работы в режимх
	+ windows service 
	+ windows console 
	+ linux daemon

+ без дополнительной параметризации
+ без дополнительных библиотек

Winux принимает сигналы завершния работы
	+ linux SIG_TERM 
	+ windows SERVICE_STOPPED (в режиме сервиса NT)
	+ windows Console Break (в режиме консоли или отладчика)

+ безавариано останавливает работу

Для теста:
http://127.0.01:8081