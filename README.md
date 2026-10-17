# Open Emulator

[![Windows build and installer](https://github.com/srviniaviz/open-emulator/actions/workflows/windows.yml/badge.svg)](https://github.com/srviniaviz/open-emulator/actions/workflows/windows.yml)

Pequeno launcher Windows para abrir e criar emuladores Android sem abrir o Android Studio. Interface nativa em C# / Windows Forms, sem dependências NuGet. Usa o .NET Framework 4.8 incluído no Windows 10/11 atualizado.

## Instalar

Baixe o arquivo `OpenEmulator-<versão>-Setup.exe` na [última release](https://github.com/srviniaviz/open-emulator/releases/latest). O instalador instala apenas para o usuário atual, sem pedir administrador, e cria um atalho no menu Iniciar. O atalho na área de trabalho é opcional. Para remover, use as configurações de Aplicativos do Windows.

Também está disponível um ZIP portátil: extraia todos os arquivos para a mesma pasta e abra `OpenEmulator.exe`. As releases incluem checksums SHA-256. Os binários não têm assinatura digital.

Requer Windows 10/11, .NET Framework 4.8 e um SDK Android com o Emulator instalado. O instalador não inclui o SDK nem imagens Android. A desinstalação do launcher preserva as configurações pessoais e os dispositivos do SDK.

## Usar

Execute `bin/OpenEmulator.exe`. Mantenha `OpenEmulator.exe.config` ao lado do executável: ele configura o runtime e o suporte a caminhos longos das ferramentas Android.

- O painel fica no canto inferior direito da área útil da tela principal, acima da barra de tarefas.
- Selecione um emulador e clique em **Abrir**, ou dê duplo clique.
- **Inicialização a frio** inicia sem carregar o snapshot; preserva os dados do dispositivo.
- **Sempre visível** mantém o painel acima das outras janelas; desmarque se preferir.
- O **X** recolhe para a bandeja. Clique no ícone para mostrar/ocultar; botão direito → **Sair** encerra o launcher. Os emuladores continuam rodando.
- O Windows pode colocar o ícone no menu de ícones ocultos. Arraste-o para perto do relógio se quiser mantê-lo visível.

## Criar dispositivos

Em **Criar…**, escolha um nome, uma imagem instalada no SDK e um modelo de dispositivo. Dispositivos existentes nunca são sobrescritos. O novo AVD aparece também no Android Studio.

Se faltarem as ferramentas de criação, o botão **Instalar ferramentas de criação** baixa o pacote estável oficial do Google (~150 MB), verifica o checksum publicado e instala em `<SDK>/cmdline-tools/open-emulator`. Não substitui outras instalações. Requer acesso de escrita ao SDK, internet e Java; o app procura `JAVA_HOME` e depois o Java do Android Studio em sua localização padrão. A instalação atual foi validada com Java 21.

Esta versão reutiliza as imagens já instaladas; não baixa outras versões Android nem aceita licenças automaticamente. Para adicionar imagens, use o SDK Manager. Para selecionar outro SDK, clique em **SDK…**. A escolha é salva em `%LOCALAPPDATA%/OpenEmulator/sdk.txt`. Sem configuração salva, procura `ANDROID_HOME`, `ANDROID_SDK_ROOT` e `%LOCALAPPDATA%/Android/Sdk`.

O painel permanece aberto; não existe configuração de inicialização automática com o Windows. É possível criar um atalho do executável manualmente na pasta de inicialização, se desejado.

## Compilar e verificar

```powershell
./build.ps1 -Test
./bin/Tests.exe --integration
./bin/Tests.exe --ui
```

O build usa o compilador do .NET Framework instalado no Windows e gera `bin/OpenEmulator.exe` e seu `.config`. Os testes básicos verificam nomes, transmissão de argumentos via CreateProcess, lançamento normal/a frio e identificação de imagens completas. Usam um processo de teste isolado para verificar o comando de abertura.

`--integration` requer SDK, ferramentas de criação e Java. Lista os AVDs existentes, muda `ANDROID_AVD_HOME` somente no processo de teste, cria um AVD com uma imagem instalada e perfil Pixel 5, verifica duplicatas e remove a pasta temporária no final. Não inicia nem modifica os AVDs existentes.

`--ui` abre brevemente o painel, verifica carregamento da lista, posição e comportamento do X, e salva `bin/preview.png`. Requer sessão gráfica com pelo menos um AVD instalado. O boot completo de um Android real não faz parte desses testes.

As ferramentas oficiais podem ser instaladas pelo próprio app ou, para diagnóstico, por `./bin/Tests.exe --install`.

## Gerar instalador e publicar versões

Instale [Inno Setup 6](https://jrsoftware.org/isinfo.php), compile e empacote:

```powershell
./build.ps1 -Test
./package.ps1
```

`dist/` recebe o instalador, o ZIP portátil (somente app, configuração e README) e os checksums. Se o compilador estiver em outra pasta, informe `./package.ps1 -CompilerPath 'C:/caminho/ISCC.exe'`. Para compilar sem substituir o executável em uso, use `./build.ps1 -Test -OutputDirectory ./bin/check` e `./package.ps1 -BuildDirectory ./bin/check`.

O [GitHub Actions](https://github.com/srviniaviz/open-emulator/actions/workflows/windows.yml) roda em pushes para `main`, pull requests e execução manual. Compila, executa os testes básicos, gera os pacotes e testa instalação e desinstalação em um runner Windows descartável. Os downloads ficam no artefato `OpenEmulator-Windows`, mantido por 30 dias. Os testes de integração com o SDK e da interface continuam locais: o CI não inicia emuladores Android.

Para uma release, atualize as versões em `src/AssemblyInfo.cs`, faça commit e envie uma tag `v<versão>`, por exemplo `v0.1.0`. O CI verifica se a tag corresponde à versão do executável e só publica a release com os arquivos depois de passar pelos testes. Releases permanecem disponíveis após a expiração dos artefatos do CI.

## Referências

- [Abrir emuladores pela linha de comando](https://developer.android.com/studio/run/emulator-commandline)
- [Criar AVDs com avdmanager](https://developer.android.com/tools/avdmanager)

O launcher usa as ferramentas presentes no SDK local (`emulator.exe` e `avdmanager`). A documentação atual também oferece a nova Android CLI; ela não é necessária para esta versão.
