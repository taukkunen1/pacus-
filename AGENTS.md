# PACUS

- O frontend oficial está em `flutter_app/`. Leia `docs/PROPOSITO.md` antes de decisões de produto e preserve as regras de autonomia e reforço positivo.
- O cronômetro usa `lib/ui/timer_alarm.dart` e `timer_alarm_controls.dart`. O alarme usa volume fixo de 100% no aplicativo. O lembrete visual aparece a cada 15 minutos na tela inicial enquanto há tempo de jogo disponível e nenhuma sessão iniciada ou pausada.
- O alarme de término contém 30 segundos contínuos de áudio. Preserve a parada automática, a interrupção manual, a liberação do player ao sair da tela e o aviso mesmo quando a API estiver lenta.
- Valide alterações do cronômetro com `flutter test`, `flutter analyze --no-fatal-infos` e `flutter build web --release`. Erros e warnings continuam bloqueantes; os avisos informativos de migração do Material permanecem visíveis. Testes com áudio simulado não comprovam reprodução real nem contornam restrições de autoplay do navegador.
- Registre alterações, validações e limitações em `docs/REGISTRO_ATUALIZACOES.md`. Preserve as instruções de branch e publicação em `CLAUDE.md`.
