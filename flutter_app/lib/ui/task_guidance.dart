// Sugestões curtas para a rotina infantil. O título exibido é a referência.
({String label, String text}) taskGuidance(
  String title, {
  String? reason,
  String? description,
}) {
  final name = title.toLowerCase().trim().replaceAll(RegExp(r'[.!?]+$'), '')
      .replaceAll(RegExp(r'\s+'), ' ');
  final suggestion = switch (name) {
    'jantar' => 'O jantar repõe a energia que você usou durante o dia.',
    'tomar banho' => 'O banho ajuda a tirar o suor e a sujeira do dia.',
    'escovar os dentes' => 'A escova tira os restos de comida e ajuda a proteger seus dentes de cáries.',
    'tirar a roupa suja da escola' || 'tirar a roupa suja da mochila' =>
      'Coloque a roupa suja no cesto para ela ficar pronta para lavar.',
    'mostrar agenda + cadernos de lições' || 'mostrar agenda e cadernos de lições' =>
      'Mostre a agenda e os cadernos a um adulto para verem juntos se há recados ou lição.',
    'depois do banho, escolher um livro e ler por 5 minutos' =>
      'Depois do banho, pegue um livro de que você gosta e escolha uma página para começar.',
    'ler o livro antes de dormir' || 'ler antes de dormir' =>
      'Escolha uma página ou um trecho curto do seu livro para ler antes de deitar.',
    'desenhar na lousa - o humor de hoje' || 'desenhar na lousa — o humor de hoje' =>
      'Escolha uma cor e faça um traço para começar seu desenho.',
    'escrever na lousa' =>
      'Escolha uma palavra que você queira escrever e comece pela primeira letra.',
    _ => null,
  };
  final custom = (reason ?? '').trim();
  final instructions = (description ?? '').trim().toLowerCase();
  if (suggestion != null && suggestion.toLowerCase() != instructions) {
    return (label: 'Dica', text: suggestion);
  }
  if (custom.isNotEmpty && custom.toLowerCase() != instructions && suggestion == null) {
    return (label: 'Por quê', text: custom);
  }
  return (
    label: 'Como começar',
    text: 'Escolha o primeiro passo de “${title.trim()}”. Se precisar, peça ajuda.',
  );
}
