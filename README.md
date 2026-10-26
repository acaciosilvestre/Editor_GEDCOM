# Editor GEDCOM em VB.NET

Projeto Windows Forms em VB.NET direcionado ao **.NET 10 (LTS)**, adequado ao Visual Studio 2026.

## Requisitos
- Windows
- Visual Studio 2026 com a carga de trabalho **Desenvolvimento para desktop com .NET**
- SDK do .NET 10 instalado (o Visual Studio normalmente instala a versão compatível junto com as ferramentas)

## Como verificar o SDK instalado

1. Abra o **Terminal**, o **Prompt de Comando** ou o **PowerShell** do Windows.
2. Execute:

   ```powershell
   dotnet --list-sdks
   ```

3. Confira se aparece uma versão que começa com `10.0.` (por exemplo, `10.0.100`). Se quiser ver também os runtimes instalados, execute `dotnet --list-runtimes`.
4. Se `dotnet` não for reconhecido ou não houver SDK `10.0.x`, abra o **Visual Studio Installer**, selecione Visual Studio 2026 > **Modificar** e confirme a carga de trabalho **Desenvolvimento para desktop com .NET** e os componentes do SDK do .NET 10. Como alternativa, instale o SDK pelo site oficial: https://dotnet.microsoft.com/download/dotnet/10.0
5. Para conferir qual versão do Visual Studio está instalada: **Ajuda > Sobre o Microsoft Visual Studio**.

> Importante: `dotnet --list-sdks` mostra os SDKs disponíveis no PATH da instalação de .NET usada pelo terminal. O Visual Studio também instala sua própria cópia do SDK. Se houver divergência, abra o terminal integrado do Visual Studio e execute o comando novamente.

## Como executar
1. Extraia o ZIP para uma pasta.
2. Abra `EditorGEDCOM.vbproj` no Visual Studio 2026.
3. Aguarde a restauração, compile e execute.

O programa carrega um GEDCOM de demonstração ao iniciar. Use **Carregar GEDCOM** para abrir outro arquivo.

## Funcionalidades
- Carregar `.ged` e `.gedcom`
- Listar e pesquisar indivíduos
- Editar nome, sexo e eventos
- Editar eventos familiares, adicionar pessoas, vínculos parentais, cônjuges e filhos
- Salvar o GEDCOM completo
- Exportar ascendentes diretos de `@I1@`
- Exportar ascendentes, filhos, famílias dos filhos, cônjuges e filhos desses casais
- Validar o GEDCOM carregado, com relatório de erros de estrutura, referências inexistentes, vínculos familiares inconsistentes e referências duplicadas

## Validação GEDCOM

Use o botão **Validar GEDCOM** depois de carregar um arquivo. O relatório verifica, entre outros itens:
- existência e posição dos registros `HEAD` e `TRLR`;
- linhas malformadas, saltos de nível e conteúdo depois de `TRLR`;
- identificadores duplicados;
- referências `FAMC`, `FAMS`, `HUSB`, `WIFE` e `CHIL` que apontam para registros inexistentes;
- coerência recíproca entre os vínculos de indivíduos e famílias, inclusive no estado atual em memória.

A validação é diagnóstica: ela não altera nem corrige automaticamente o arquivo. Para validar mudanças feitas no editor, salve-as e carregue novamente o arquivo salvo, pois a análise estrutural usa o conteúdo do último arquivo carregado.

## Observações
Esta é uma conversão funcional da lógica central para Windows Forms, não uma tradução visual pixel a pixel do HTML. A interface foi reorganizada em abas. O parser preserva os campos GEDCOM contemplados pelo editor original; tags e estruturas que não são representadas pelo modelo interno podem não ser preservadas na exportação. Para arquivos de produção, recomenda-se testar com uma cópia e validar o GEDCOM exportado.

## Visualização da árvore genealógica

A barra superior inclui o botão **Árvore genealógica**. Selecione uma pessoa na lista e abra a árvore para visualizar três abas:

- **Ascendentes:** pais, avós e gerações anteriores, usando os vínculos familiares registrados.
- **Descendentes:** cônjuges, famílias e filhos em gerações sucessivas.
- **Relações familiares:** pais, irmãos, cônjuges e filhos imediatos.

Dê duplo clique sobre uma pessoa na árvore para selecioná-la no editor principal. A visualização não altera os dados GEDCOM. Para evitar recursão infinita em arquivos com vínculos circulares, a navegação limita a profundidade e indica ciclos detectados.

A árvore reflete os vínculos presentes no modelo carregado; vínculos ausentes no GEDCOM não podem ser inferidos automaticamente.
