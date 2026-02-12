# Contribuindo para o VPS File Manager

Primeiramente, obrigado por considerar contribuir! 🎉

## Como Contribuir

### Reportando Bugs

1. Verifique se o bug já não foi reportado em [Issues](../../issues)
2. Se não encontrar, [abra uma nova issue](../../issues/new)
3. Inclua:
   - Versão do Windows
   - Versão do app
   - Passos para reproduzir
   - Comportamento esperado vs atual
   - Screenshots (se aplicável)

### Sugerindo Melhorias

1. Abra uma [issue](../../issues/new) com a tag `enhancement`
2. Descreva claramente a funcionalidade
3. Explique por que seria útil

### Pull Requests

1. Fork o repositório
2. Crie uma branch: `git checkout -b feature/minha-feature`
3. Faça suas alterações
4. Teste localmente
5. Commit: `git commit -m 'Add: descrição da mudança'`
6. Push: `git push origin feature/minha-feature`
7. Abra um Pull Request

### Padrões de Código

- Use **C# 10** conventions
- Siga o padrão **MVVM**
- Nomeie variáveis em inglês
- Comente código complexo
- Mantenha os métodos pequenos e focados

### Commits

Use prefixos nos commits:

- `Add:` - Nova funcionalidade
- `Fix:` - Correção de bug
- `Update:` - Atualização de funcionalidade existente
- `Remove:` - Remoção de código/funcionalidade
- `Refactor:` - Refatoração sem mudança de comportamento
- `Docs:` - Apenas documentação
- `Style:` - Formatação, sem mudança de lógica

### Estrutura do Projeto

```
VPSFileManager/
├── Controls/      # Controles WPF customizados
├── Converters/    # Conversores XAML
├── Models/        # Entidades de dados
├── Services/      # Lógica de negócio
├── Terminal/      # Emulador VT100
├── ViewModels/    # MVVM ViewModels
├── Views/         # Interfaces XAML
└── Themes/        # Estilos
```

## Ambiente de Desenvolvimento

### Requisitos

- Visual Studio 2022 ou VS Code
- .NET Framework 4.8 SDK
- Windows 10/11

### Build

```powershell
cd VPSFileManager
dotnet build
```

### Executar

```powershell
dotnet run
```

## Dúvidas?

Abra uma [Discussion](../../discussions) ou entre em contato via Issues.

Obrigado! 🚀
