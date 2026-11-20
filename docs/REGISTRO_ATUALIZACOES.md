# Registro de atualizações

## 2026-11-20 — Volume e alarme do cronômetro

- Implementado controle de volume de 0% a 100%, com padrão de 80%, persistido neste navegador/aparelho. O controle aparece antes e durante a sessão e no aviso de término. Volume zero mantém o aviso visual.
- Substituídos os três sons curtos por um WAV sintetizado com 30 segundos contínuos, sem dependência de downloads de áudio. Incluídos teste de som de dois segundos, parada manual, parada automática e liberação do player ao sair da tela.
- O alarme começa no término da contagem independentemente da resposta da API. O modal não espera o alarme terminar. Fechar o modal também interrompe o áudio.
- Arquivos principais: `flutter_app/lib/screens/home_screen.dart`, `flutter_app/lib/ui/timer_alarm.dart`, `flutter_app/lib/ui/timer_alarm_controls.dart` e `flutter_app/test/timer_alarm_test.dart`. Regras duráveis documentadas em `AGENTS.md`.
- Validação local: Flutter 3.50.1 / Dart 3.14.0; `flutter test` passou com 31 testes, incluindo sete novos testes de áudio/cronômetro; `flutter build web --release --dart-define=PACUS_API_BASE_URL=https://pacus-pacus-api.fly.dev/api/v1` passou; `git diff --check` passou.
- `flutter analyze` encontrou avisos informativos sobre imports Material obsoletos no Flutter atual. Removido um import desnecessário introduzido nesta alteração. `flutter analyze --no-fatal-infos` passou com 19 avisos informativos e sem erros ou warnings. Os três workflows Flutter usam essa opção, mantendo erros e warnings bloqueantes e os avisos informativos visíveis.
- Limitações: reprodução física e autoplay em navegadores reais não foram testados; restrições do navegador, aba fechada ou volume do sistema podem impedir som. Os testes usam player simulado e também conferem duração e ausência de lacunas no WAV gerado.
- Nesta etapa, implementação validada localmente e preparada para revisão no GitHub. Publicação em produção ainda não confirmada. O arquivo de auditoria preexistente não integra esta alteração.
