# MicroMerge - Implementação de Junção Sort-Merge

Uma implementação em C# da operação **Junção Sort-Merge** usando Ordenação Externa (External Merge Sort) para processamento de grandes volumes de dados com **restrições rigorosas de memória**.

## 📋 Índice

- [Visão Geral](#visão-geral)
- [Características](#características)
- [Arquitetura](#arquitetura)
- [Instalação](#instalação)
- [Como Usar](#como-usar)
- [Esquema de Dados](#esquema-de-dados)
- [Detalhes do Algoritmo](#detalhes-do-algoritmo)
- [Gerenciamento de Memória](#gerenciamento-de-memória)
- [Métricas de Performance](#métricas-de-performance)
- [Exemplos](#exemplos)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Contribuindo](#contribuindo)

## 🎯 Visão Geral

MicroMerge implementa a operação **Junção Sort-Merge** da álgebra relacional, especificamente projetada para processar grandes conjuntos de dados que não cabem inteiramente na memória. A implementação usa **Ordenação Externa (External Merge Sort)** para manipular dados de forma eficiente mantendo **restrições rigorosas de memória**.

### ⚠️ **RESTRIÇÃO CRÍTICA DE MEMÓRIA**:

- **APENAS 4 PÁGINAS (40 registros) NA MEMÓRIA A QUALQUER MOMENTO**
- **NUNCA excede este limite, independente do tamanho dos dados de entrada**
- **Processamento page-by-page para garantir conformidade com a restrição**

### Características Principais:

- **Restrição de Memória**: Usa APENAS 4 páginas (40 registros) na memória simultaneamente
- **Ordenação Externa**: Processa datasets maiores que a memória disponível
- **I/O Eficiente**: Leitura e escrita otimizada baseada em páginas
- **Merge Multi-Pass**: Automaticamente processa grandes quantidades de runs ordenados

## ✨ Características

- 🔄 **Junção Sort-Merge**: Operação de inner join eficiente entre duas tabelas
- 💾 **Ordenação Externa**: Processa grandes datasets com **limitação rigorosa de memória**
- 📊 **Rastreamento de Performance**: Estatísticas detalhadas de I/O e uso de memória
- 🔍 **Suporte Multi-Tabelas**: Funciona com datasets de vinhos, uvas e países
- 📁 **Exportação CSV**: Resultados podem ser exportados para formato CSV
- ⚡ **Performance Otimizada**: Otimização automática do tamanho das tabelas (tabela maior torna-se operando esquerdo)

## 🏗️ Arquitetura

### Componentes Principais

1. **Operator**: Classe principal que orquestra a operação de junção sort-merge
2. **Tables**: Estruturas de dados representando tabelas de vinhos, uvas e países
3. **Records**: Classes de registro tipadas para cada tipo de tabela
4. **External Merge Sort**: Algoritmo de ordenação eficiente em memória
5. **Page Management**: Sistema de páginas de tamanho fixo (10 registros por página)

### Fluxo do Algoritmo

```
Tabelas de Entrada (Esquerda, Direita)
         ↓
   Ordenação Externa (External Merge Sort)
         ↓
   Criação de Runs Ordenados
         ↓
   Merge Multi-Way
         ↓
   Junção Sort-Merge
         ↓
   Tabela Resultado + Métricas
```

### 🚨 **GERENCIAMENTO RIGOROSO DE MEMÓRIA**

```
MEMÓRIA TOTAL: APENAS 4 PÁGINAS (40 registros)
├── Fase de Ordenação: 4 páginas para acumulação e ordenação
├── Fase de Merge: 3 páginas de entrada + 1 página de saída
└── Fase de Junção: 2 páginas de entrada + 1 página de marca + 1 página de saída

⚠️  NUNCA excede 4 páginas independente do tamanho dos dados!
```

## 🛠️ Instalação

### Pré-requisitos

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Sistema Operacional: Windows, macOS, ou Linux

### Configuração

1. **Clone o repositório**:

    ```bash
    git clone <repository-url>
    cd SubMerge/MicroMerge
    ```

2. **Restaure as dependências**:

    ```bash
    dotnet restore
    ```

3. **Compile o projeto**:
    ```bash
    dotnet build
    ```

## 🚀 Como Usar

### Execução Básica

Execute a aplicação com configuração padrão:

```bash
dotnet run --project MicroMerge
```

### Opções de Configuração

A aplicação pode ser configurada modificando o arquivo `Program.cs` para usar diferentes operações de junção:

```csharp
// Diferentes combinações de junção (descomente a operação desejada):

// Vinho ⋈ País (por país de produção)
using var op = new Operator(wineTable, countryTable, "pais_producao_id", "pais_id");

// Vinho ⋈ Uva (por tipo de uva)
using var op = new Operator(wineTable, grapeTable, "uva_id", "uva_id");

// Uva ⋈ País (por país de origem) - Padrão
using var op = new Operator(countryTable, grapeTable, "pais_id", "pais_origem_id");
```

### Saída

A aplicação irá:

1. Exibir estatísticas da operação
2. Criar um arquivo CSV de resultado no diretório `output/`
3. Mostrar uso de memória e contagem de operações de I/O

Exemplo de saída:

```
Operação concluída com sucesso!
Result:
    Número de páginas geradas: 8
    Número de registros gerados: 73
    Número de IO's: 82
    Nome da tabela gerada: grapes_countries_joined_sorted_pais_origem_id
```

## 📊 Esquema de Dados

### Estrutura das Tabelas

#### Tabela Vinho (`vinho.csv`)

| Coluna             | Tipo   | Descrição                |
| ------------------ | ------ | ------------------------ |
| `vinho_id`         | int    | Chave Primária           |
| `rotulo`           | string | Rótulo do vinho          |
| `ano_producao`     | int    | Ano de produção          |
| `uva_id`           | int    | Chave Estrangeira → Uva  |
| `pais_producao_id` | int    | Chave Estrangeira → País |

#### Tabela Uva (`uva.csv`)

| Coluna           | Tipo   | Descrição                |
| ---------------- | ------ | ------------------------ |
| `uva_id`         | int    | Chave Primária           |
| `nome`           | string | Nome da uva              |
| `tipo`           | string | Tipo da uva              |
| `ano_colheita`   | int    | Ano de colheita          |
| `pais_origem_id` | int    | Chave Estrangeira → País |

#### Tabela País (`pais.csv`)

| Coluna    | Tipo   | Descrição      |
| --------- | ------ | -------------- |
| `pais_id` | int    | Chave Primária |
| `nome`    | string | Nome do país   |
| `sigla`   | string | Código do país |

### Modelo Entidade-Relacionamento

```
País (Country)
├── pais_id (PK)
├── nome
└── sigla

Uva (Grape)
├── uva_id (PK)
├── nome
├── tipo
├── ano_colheita
└── pais_origem_id (FK → País.pais_id)

Vinho (Wine)
├── vinho_id (PK)
├── rotulo
├── ano_producao
├── uva_id (FK → Uva.uva_id)
└── pais_producao_id (FK → País.pais_id)
```

## 🧮 Detalhes do Algoritmo

### Ordenação Externa (External Merge Sort)

A implementação usa uma **ordenação externa de 2 fases** com **restrição rigorosa de memória**:

#### Fase 1: Criação de Runs

- Lê dados em chunks que cabem na memória (**APENAS 4 páginas = 40 registros**)
- Ordena cada chunk na memória
- Escreve runs ordenados em arquivos temporários
- **NUNCA excede 4 páginas na memória**

#### Fase 2: Merge Multi-Way

- Faz merge dos runs ordenados usando abordagem tournament tree
- Mantém **APENAS 1 página por run de entrada + 1 página de saída na memória**
- Processa qualquer quantidade de runs através de merge multi-pass
- **GARANTE que nunca mais de 4 páginas estão na memória simultaneamente**

### Junção Sort-Merge

1. **Preparação**: Ambas as tabelas são ordenadas externamente pelas colunas de junção
2. **Processo de Merge**: Traversal simultânea das tabelas ordenadas
3. **Tratamento de Duplicatas**: Produto cartesiano adequado para valores de junção duplicados
4. **Gerenciamento de Memória**: Usa sistema de buffer de 4 páginas

### 🚨 **CONSTRAINT CRÍTICA DE MEMÓRIA**

```
┌─────────────────────────────────────────────────┐
│  RESTRIÇÃO ABSOLUTA: MÁXIMO 4 PÁGINAS (40 registros) │
├─────────────────────────────────────────────────┤
│  ✓ Fase de Ordenação: 4 páginas máximo          │
│  ✓ Fase de Merge: 3 entrada + 1 saída           │
│  ✓ Fase de Junção: 2 entrada + 1 marca + 1 saída│
│  ✓ NUNCA excede independente do tamanho dos dados│
└─────────────────────────────────────────────────┘
```

## 💾 Gerenciamento de Memória

### Alocação de Buffer

O sistema mantém restrições rigorosas de memória:

| Operação      | Uso de Buffer                 | Descrição                        |
| ------------- | ----------------------------- | -------------------------------- |
| **Ordenação** | 4 páginas máx                 | Acumular, ordenar, escrever runs |
| **Merging**   | 3 entrada + 1 saída           | Buffer de merge multi-way        |
| **Junção**    | 2 entrada + 1 marca + 1 saída | Buffer de junção sort-merge      |

### Sistema de Páginas

- **Tamanho da Página**: 10 registros por página
- **Limite de Memória**: Máximo 4 páginas (40 registros) na memória
- **Unidade de I/O**: Todas as operações de disco trabalham com páginas completas

### 🔒 **GARANTIAS DE MEMÓRIA**

```
⚠️  GARANTIAS IMPLEMENTADAS:

1. RunIterator: 1 página por iterador
2. CreateSortedRuns: Máximo 4 páginas para acumulação
3. MergeRuns: Máximo 3 páginas de entrada + 1 saída
4. SortMergeJoin: 2 entrada + 1 marca + 1 saída
5. Multi-pass: Quando necessário, divide automaticamente

RESULTADO: NUNCA excede 4 páginas independente do tamanho dos dados!
```

## 📈 Métricas de Performance

A aplicação rastreia e reporta:

- **Páginas Criadas**: Número de páginas escritas no disco
- **Registros Gerados**: Total de registros no resultado
- **Operações de I/O**: Contagem de operações de leitura/escrita no disco
- **Uso de Memória**: Permanece dentro da restrição de 4 páginas

### Otimização de Performance

- **Ordenação de Tabelas**: Tabela maior automaticamente torna-se operando esquerdo
- **Minimização de Runs**: Criação eficiente de runs reduz passes de merge
- **Gerenciamento de Buffer**: Estratégia ótima de substituição de páginas

## 💡 Exemplos

### Exemplo 1: Junção Uva-País

Encontrar todas as uvas com seus países de origem:

```csharp
var countryTable = new CountryTable("./Data/pais.csv");
var grapeTable = new GrapeTable("./Data/uva.csv");

using var op = new Operator(countryTable, grapeTable, "pais_id", "pais_origem_id");
var result = op.Execute();
op.WriteToCsv("./output");
```

**Resultado**: Arquivo CSV com informações de uvas unidos com detalhes dos países.

### Exemplo 2: Junção Vinho-Uva

Encontrar todos os vinhos com suas informações de uvas:

```csharp
var wineTable = new WineTable("./Data/vinho.csv");
var grapeTable = new GrapeTable("./Data/uva.csv");

using var op = new Operator(wineTable, grapeTable, "uva_id", "uva_id");
var result = op.Execute();
```

### Exemplo 3: Análise de Performance

```csharp
var result = op.Execute();
Console.WriteLine($"Operações de I/O: {result.NumberOfIOOperations}");
Console.WriteLine($"Eficiência de Memória: {result.NumberOfCreatedRecords / result.NumberOfIOOperations:F2} registros/I/O");
Console.WriteLine($"Páginas na memória: MÁXIMO 4 páginas (garantido)");
```

## 📁 Estrutura do Projeto

```
MicroMerge/
├── Program.cs                    # Ponto de entrada da aplicação
├── MicroMerge.csproj            # Configuração do projeto
├── Data/                        # Arquivos CSV de entrada
│   ├── pais.csv                 # Dados dos países
│   ├── uva.csv                  # Dados das uvas
│   └── vinho.csv                # Dados dos vinhos
├── Models/                      # Modelos de dados
│   ├── Page.cs                  # Estrutura de página
│   ├── PageId.cs                # Identificador de página
│   └── Record/                  # Tipos de registro
│       ├── Record.cs            # Classe base de registro
│       ├── CountryRecord.cs     # Registro de país
│       ├── GrapeRecord.cs       # Registro de uva
│       └── WineRecord.cs        # Registro de vinho
├── Tables/                      # Implementações de tabela
│   ├── Table.cs                 # Classe base de tabela
│   ├── SortedTable.cs           # Tabela ordenada com I/O de disco
│   ├── CountryTable.cs          # Carregador da tabela de países
│   ├── GrapeTable.cs            # Carregador da tabela de uvas
│   └── WineTable.cs             # Carregador da tabela de vinhos
├── Operator/                    # Operação de junção
│   └── Operator.cs              # Implementação da junção sort-merge
└── output/                      # Resultados gerados
    └── *.csv                    # Resultados das junções
```

## 🧪 Testes

### Testes Manuais

1. **Execute com diferentes operações de junção**:

    ```bash
    # Edite Program.cs para descomentar a junção desejada
    dotnet run --project MicroMerge
    ```

2. **Verifique os arquivos de saída**:

    ```bash
    ls -la output/
    cat output/grapes_countries_joined_sorted_pais_origem_id.csv
    ```

3. **Teste de performance**:
    - Monitore a contagem de operações de I/O
    - Verifique conformidade com restrição de memória
    - Confira correção da junção

### Validação

- **Correção**: Compare resultados da junção com resultados esperados
- **Conformidade de Memória**: Certifique-se de nunca exceder limite de 4 páginas
- **Eficiência de I/O**: Monitore contagem de operações de disco

## 📄 Licença

Este projeto faz parte de uma tarefa acadêmica para o curso de Sistemas de Banco de Dados (SGBD).

## 🔗 Referências

- Conceitos de Sistema de Banco de Dados (Silberschatz, Galvin, Gagne)
- Algoritmos de Ordenação Externa
- Padrões de Implementação de Junção Sort-Merge

---

**Autores**: Curso de Sistemas de Banco de Dados - UFC  
**Data**: Junho 2025  
**Versão**: 1.0.0
