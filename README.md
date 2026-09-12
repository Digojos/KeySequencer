# Key Sequencer

App desktop (Windows) para disparar uma sequência configurável de teclas com uma única
tecla de atalho global. Você define a lista de teclas na ordem que quiser (ex: `F1`, `F3`,
`F2`, `ALT+A`) e um atraso entre elas; ao apertar o hotkey em qualquer lugar do sistema
(mesmo com outra janela ou jogo em foco), a sequência é executada automaticamente.

Derivado do [combo-app](../combo-app) (uma ferramenta similar voltada especificamente para
Dota 2), generalizado para qualquer sequência de teclas.

## Pré-requisitos

- Windows 10 ou superior (usa APIs do Win32 - `SendInput`, hook de teclado - então não
  roda em Linux/macOS)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) para compilar/rodar a
  partir do código-fonte

Se você só quer *usar* o app (sem compilar), peça a quem gerou o executável um publish
autocontido (`dotnet publish -r win-x64 --self-contained`) - nesse caso não precisa do
.NET instalado, só do próprio `.exe`.

## Rodando a partir do código-fonte

```powershell
dotnet build KeySequencer.sln
dotnet run --project src/KeySequencer.UI
```

## Publicando (gerar o `.exe` para distribuir)

Não há instalador configurado - a forma mais simples de compartilhar o app é gerar um
`.exe` autocontido (inclui o runtime do .NET embutido, então quem for rodar não precisa
ter o .NET instalado):

```powershell
dotnet publish src/KeySequencer.UI -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None
```

O resultado fica em `src/KeySequencer.UI/bin/Release/net8.0/win-x64/publish/`. Com essas
três flags juntas (`PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract` +
`DebugType=None`) o `.exe` (~90 MB, por embutir o runtime do .NET e as bibliotecas
nativas do Avalonia/SkiaSharp) é o único arquivo necessário - sem `PublishSingleFile`
ficam vários `.dll` soltos ao lado; sem `IncludeNativeLibrariesForSelfExtract` as
bibliotecas nativas (`libSkiaSharp.dll` etc.) continuam separadas mesmo com
`PublishSingleFile`. Alguns `.pdb` de bibliotecas de terceiros ainda aparecem na pasta
(símbolos de depuração) - não são necessários para rodar, pode ignorá-los ou apagá-los
antes de compartilhar.

É só copiar/zipar esse `.exe` e mandar. Por não ser assinado digitalmente, o Windows
SmartScreen provavelmente vai avisar "Editor desconhecido" no primeiro run em outra
máquina - clicar em "Mais informações" → "Executar assim mesmo" resolve; é esperado para
uma ferramenta pessoal não publicada/assinada.

## Como usar

1. **Tecla de disparo**: escolha a tecla (e opcionalmente Ctrl/Alt) que vai armar a
   sequência. Padrão: `SPACE`.
2. **Sequência de teclas**: uma tecla por linha, na ordem de execução. Combine
   modificadores com `+` (ex: `ALT+A`, `CTRL+Q`).
3. **Ping (ms)**: atraso entre cada tecla da sequência.
4. Clique em **LIGADO/DESLIGADO** para armar. Com o app **DESLIGADO**, a tecla de
   disparo funciona normalmente em qualquer lugar (inclusive para digitar); só quando
   está **LIGADO** ela é capturada como gatilho e não chega mais ao Windows como uma
   tecla normal.
5. **Gerar prévia** mostra a sequência resolvida sem executá-la de verdade.
6. **Salvar** abre o diálogo nativo do Windows para escolher onde salvar o JSON (sugere a
   pasta `configs/` ao lado do `.exe`, mas você pode escolher qualquer local). **Importar**
   abre o mesmo tipo de diálogo para carregar um arquivo salvo.

Minimizar a janela ou fechar no X manda o app para a bandeja do sistema (o hotkey global
continua ativo em segundo plano). Para encerrar de verdade, use "Sair" no menu da bandeja.

## Limitações conhecidas

- Se o app-alvo (jogo, etc.) estiver rodando como administrador e este app não, o Windows
  (UIPI) bloqueia silenciosamente o hook/`SendInput` de afetar aquela janela - rode este
  app como administrador também nesse caso.
- Só testado em Windows; não há suporte a outras plataformas.
