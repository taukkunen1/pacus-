# Registro de atualizações

## 2026-11-27 — Volume fixo, lembrete de tempo de jogo e chat

- O alarme usa volume fixo de 100% no aplicativo. Removido o controle de volume em todas as telas do cronômetro, e preferências antigas deixam de ser usadas.
- A tela inicial mostra um lembrete visual a cada 15 minutos quando há tempo de jogo disponível e nenhuma sessão ativa ou pausada. O aviso leva ao cronômetro para escolher a duração.
- O chat da criança deixa de exibir o bloco “Precisa de apoio?” e as respostas sugeridas. A conversa continua pelo campo de mensagem.
- Os testes cobrem o volume fixo mesmo com uma preferência antiga de mudo e a recorrência do lembrete. A reprodução física continua sujeita ao volume e às permissões do dispositivo.

## 2026-11-27 — Minha rotina como única visualização das tarefas

- A tela inicial mostra diretamente a lista agrupada por manhã, tarde e noite para crianças e adultos. Foram removidos o cartão “Próxima tarefa” e o botão que alternava para essa visualização.
- Validação estática: conferidas a remoção das referências ao modo anterior e a presença das ações de conclusão e ajuda na lista. O ambiente local não tem o SDK Flutter disponível.

## 2026-11-20 — Volume e alarme do cronômetro

- Implementado controle de volume de 0% a 100%, com padrão de 80%, persistido neste navegador/aparelho. O controle aparece antes e durante a sessão e no aviso de término. Volume zero mantém o aviso visual.
- Substituídos os três sons curtos por um WAV sintetizado com 30 segundos contínuos, sem dependência de downloads de áudio. Incluídos teste de som de dois segundos, parada manual, parada automática e liberação do player ao sair da tela.
- O alarme começa no término da contagem independentemente da resposta da API. O modal não espera o alarme terminar. Fechar o modal também interrompe o áudio.
- Arquivos principais: `flutter_app/lib/screens/home_screen.dart`, `flutter_app/lib/ui/timer_alarm.dart`, `flutter_app/lib/ui/timer_alarm_controls.dart` e `flutter_app/test/timer_alarm_test.dart`. Regras duráveis documentadas em `AGENTS.md`.
- Validação local: Flutter 3.50.1 / Dart 3.14.0; `flutter test` passou com 31 testes, incluindo sete novos testes de áudio/cronômetro; `flutter build web --release --dart-define=PACUS_API_BASE_URL=https://pacus-pacus-api.fly.dev/api/v1` passou; `git diff --check` passou.
- `flutter analyze` encontrou avisos informativos sobre imports Material obsoletos no Flutter atual. Removido um import desnecessário introduzido nesta alteração. `flutter analyze --no-fatal-infos` passou com 19 avisos informativos e sem erros ou warnings. Os três workflows Flutter usam essa opção, mantendo erros e warnings bloqueantes e os avisos informativos visíveis.
- Limitações: reprodução física e autoplay em navegadores reais não foram testados; restrições do navegador, aba fechada ou volume do sistema podem impedir som. Os testes usam player simulado e também conferem duração e ausência de lacunas no WAV gerado.
- GitHub: PR #111 integrado em main, commit f446e0ec0ef51128f2b40a826922ccb82d362081. PACUS CI (frontend e backend), Flutter Web CI, Secret scan e CodeQL passaram no commit de origem 7e8494d83bdda8f74d01ae32167e346ad07bbcbc.
- Publicação confirmada: workflow Deploy PACUS Flutter Web, execução 44818215489, concluiu com sucesso. https://www.pacus.com.br/ retornou HTTP 200; o main.dart.js servido retornou HTTP 200 e contém os novos textos de volume, duração e parada, além da chave de preferência local. Isso confirma os arquivos publicados; não substitui teste auditivo em navegador real.
- O arquivo de auditoria preexistente não integra esta alteração. O SDK Flutter foi preparado em C:/Users/pedro/.cache/pacus-flutter-sdk; arquivos gerados de build e dependências ficaram apenas no checkout local.
