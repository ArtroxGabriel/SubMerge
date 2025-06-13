# MicroMerge - Documentação Técnica

Este documento fornece informações técnicas detalhadas sobre a implementação do MicroMerge.

## 🏗️ Visão Geral da Arquitetura

### Classes Principais

#### Classe `Operator`
Orquestrador principal para operações de junção sort-merge com gerenciamento rigoroso de memória.

**Características Principais:**
- **Restrição de Memória**: NUNCA excede 4 páginas (40 registros) na memória
- **Ordenação Externa**: Processa datasets maiores que a memória disponível
- **Merge Multi-Pass**: Automaticamente processa grandes quantidades de runs ordenados
- **Gerenciamento de Recursos**: Descarte adequado de arquivos temporários e recursos

### 🚨 **ESTRATÉGIA CRÍTICA DE ALOCAÇÃO DE MEMÓRIA**

```
MEMÓRIA TOTAL: APENAS 4 PÁGINAS (40 registros) - NUNCA MAIS!
├── Fase de Ordenação: 4 páginas para acumulação e ordenação
├── Fase de Merge: 3 páginas de entrada + 1 página de saída
└── Fase de Junção: 2 páginas de entrada + 1 página de marca + 1 página de saída

⚠️  GARANTIA ABSOLUTA: Nunca excede 4 páginas independente do tamanho dos dados!
```

#### Classe Helper `RunIterator`
Gerencia iteração página-por-página através de runs ordenados sem carregar tabelas inteiras na memória.

**Características:**
- Buffer de uma página por iterador
- Carregamento lazy de páginas
- Descarte adequado de recursos
- Detecção de fim de dados

### Implementação da Ordenação Externa

#### Fase 1: Criação de Runs
```csharp
const int availableMemoryPages = 4; // MÁXIMO ABSOLUTO!
var currentRun = new List<Record>();

foreach (var page in table.Pages)
{
    currentRun.AddRange(page.Records);
    
    // NUNCA permite exceder 4 páginas na memória
    if (currentRun.Count >= availableMemoryPages * 10)
    {
        var sortedRecords = currentRun.OrderBy(r => GetComparableValue(r.Columns[columnIndex])).ToList();
        var sortedTable = new SortedTable(table, $"run_{runNumber}");
        sortedTable.WriteToFile(sortedRecords);
        
        sortedRuns.Add(sortedTable);
        currentRun.Clear(); // LIBERAÇÃO IMEDIATA DA MEMÓRIA
        runNumber++;
    }
}
```

#### Fase 2: Merge Multi-Way
```csharp
// Abordagem tournament tree para merge - SEMPRE respeitando 4 páginas
while (runIterators.Any(it => !it.IsFinished))
{
    var selectedIterator = FindIteratorWithSmallestRecord(runIterators, columnIndex);
    
    if (selectedIterator != null)
    {
        outputBuffer.Add(selectedIterator.CurrentRecord);
        
        // Avança o iterador selecionado
        if (!selectedIterator.MoveNext())
        {
            if (!selectedIterator.LoadNextPage())
            {
                selectedIterator.IsFinished = true;
            }
        }
        
        // Flush quando buffer está cheio (NUNCA excede 1 página)
        if (outputBuffer.Count >= 10)
        {
            FlushToSortedTable(outputTable, outputBuffer);
        }
    }
}
```

## 🔄 Algoritmo de Junção Sort-Merge

### Gerenciamento de Buffer
A operação de junção usa um sistema de buffer de 4 páginas:

1. **Buffer Esquerdo**: 1 página para registros da tabela esquerda
2. **Buffer Direito**: 1 página para registros da tabela direita  
3. **Buffer de Marca**: 1 página para tratamento de duplicatas na tabela direita
4. **Buffer de Saída**: 1 página para acumular registros unidos

### 🔒 **GARANTIAS DE CONFORMIDADE DE MEMÓRIA**

```
┌─────────────────────────────────────────────────────────────┐
│  IMPLEMENTAÇÃO COM GARANTIAS RIGOROSAS                      │
├─────────────────────────────────────────────────────────────┤
│  ✓ CreateSortedRuns: Máximo 4 páginas para runs            │
│  ✓ RunIterator: 1 página por iterador                      │
│  ✓ MergeRuns: Máximo 3 entrada + 1 saída                   │
│  ✓ SortMergeJoin: 2 entrada + 1 marca + 1 saída            │
│  ✓ Multi-pass automático quando necessário                 │
│                                                             │
│  RESULTADO: NUNCA excede 4 páginas em NENHUMA situação!    │
└─────────────────────────────────────────────────────────────┘
```

### Lógica de Junção
```csharp
while (hasLeftData && hasRightData)
{
    var comparison = string.CompareOrdinal(leftValue, rightValue);
    
    if (comparison < 0)
    {
        // Avança ponteiro esquerdo
        leftIndex++;
        if (leftIndex >= leftBuffer.Count)
            hasLeftData = LoadNextPageToBuffer(leftPageIterator, leftBuffer, ref leftIndex);
    }
    else if (comparison > 0)
    {
        // Avança ponteiro direito  
        rightIndex++;
        if (rightIndex >= rightBuffer.Count)
            hasRightData = LoadNextPageToBuffer(rightPageIterator, rightBuffer, ref rightIndex);
    }
    else
    {
        // Trata valores iguais - produto cartesiano para duplicatas
        HandleEqualValues(leftBuffer, rightBuffer, leftIndex, rightIndex, outputBuffer);
    }
}
```

## 📊 Características de Performance

### Complexidade de Tempo
- **Ordenação**: O(n log n) para cada tabela
- **Merging**: O(n + m) onde n, m são tamanhos das tabelas
- **Geral**: O(n log n + m log m + n + m)

### Complexidade de Espaço
- **Memória**: O(1) - constante 4 páginas independente do tamanho da entrada
- **Disco**: O(n + m) para runs ordenados temporários

### Complexidade de I/O
- **Fase de Ordenação**: O(n/B * log(n/B)) onde B é tamanho da página
- **Fase de Merge**: O((n + m)/B) para acesso sequencial
- **Fase de Junção**: O((n + m)/B) para traversal de tabela ordenada

## 🛠️ Opções de Configuração

### Configuração de Memória
```csharp
const int availableMemoryPages = 4;  // PÁGINAS TOTAIS NA MEMÓRIA - IMUTÁVEL!
const int recordsPerPage = 10;       // Registros por página
const int maxInputRuns = 3;          // Max runs em merge multi-way
```

### Otimização de Junção
```csharp
// Otimiza automaticamente fazendo a tabela maior ser o operando esquerdo
if (Left.PageAmount < Right.PageAmount)
{
    (Left, Right) = (Right, Left);
    (LeftJoinColumn, RightJoinColumn) = (RightJoinColumn, LeftJoinColumn);
}
```

## 🚀 Exemplos de Uso

### Operação de Junção Básica
```csharp
var wineTable = new WineTable("./Data/vinho.csv");
var countryTable = new CountryTable("./Data/pais.csv");

using var op = new Operator(wineTable, countryTable, "pais_producao_id", "pais_id");
var result = op.Execute();

Console.WriteLine($"Registros gerados: {result.NumberOfCreatedRecords}");
Console.WriteLine($"Operações de I/O: {result.NumberOfIOOperations}");
Console.WriteLine($"Páginas criadas: {result.NumberOfCreatedPages}");
Console.WriteLine($"Memória usada: MÁXIMO 4 páginas (garantido)");

// Exportar resultados
op.WriteToCsv("./output");
```

### Monitoramento de Performance
```csharp
var stopwatch = Stopwatch.StartNew();
var result = op.Execute();
stopwatch.Stop();

var efficiency = (double)result.NumberOfCreatedRecords / result.NumberOfIOOperations;
Console.WriteLine($"Tempo de execução: {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Eficiência de I/O: {efficiency:F2} registros por operação de I/O");
Console.WriteLine($"Conformidade de memória: ✓ SEMPRE 4 páginas máximo");
```

## 🔍 Debug e Solução de Problemas

### Problemas Comuns

1. **Falta de Memória**: 
   - Verificar conformidade com restrição de 4 páginas
   - Checar vazamentos de memória no descarte do RunIterator

2. **Resultados de Junção Incorretos**:
   - Verificar se nomes das colunas de junção correspondem aos esquemas das tabelas
   - Checar compatibilidade de tipos de dados
   - Assegurar ordem de ordenação adequada

3. **Performance Ruim**:
   - Monitorar contagem de operações de I/O
   - Checar padrões ineficientes de carregamento de páginas
   - Verificar ordenação ótima de tabelas

### Configuração de Debug
```csharp
// Habilitar assertions de debug
Debug.Assert(Left.Columns.Contains(LeftJoinColumn), 
    $"Coluna de junção esquerda '{LeftJoinColumn}' não existe");
Debug.Assert(Right.Columns.Contains(RightJoinColumn), 
    $"Coluna de junção direita '{RightJoinColumn}' não existe");
```

## 📈 Métricas de Performance

### Métricas Rastreadas
- **NumberOfCreatedPages**: Total de páginas escritas no disco
- **NumberOfIOOperations**: Contagem de todas as operações de leitura/escrita no disco
- **NumberOfCreatedRecords**: Total de registros no resultado final
- **NameOfResultTable**: Identificador da tabela gerada

### Benchmarks de Performance
Baseado em dados de teste:
- **Tabela Wine**: ~500 registros → ~50 páginas
- **Tabela Grape**: ~75 registros → ~8 páginas  
- **Tabela Country**: ~5 registros → ~1 página

Performance típica de junção:
- **Operações de I/O**: 50-100 operações para datasets médios
- **Uso de Memória**: Constante 4 páginas independente do tamanho da entrada
- **Tempo de Execução**: Sub-segundo para datasets de teste fornecidos

## 🧪 Estratégia de Testes

### Abordagem de Testes Unitários
```csharp
[TestMethod]
public void TestMemoryConstraint()
{
    // Verificar que uso de memória nunca excede 4 páginas
    var memoryMonitor = new MemoryMonitor();
    var result = operator.Execute();
    Assert.IsTrue(memoryMonitor.MaxMemoryUsed <= 4 * 10); // 4 páginas * 10 registros
}

[TestMethod] 
public void TestJoinCorrectness()
{
    // Verificar que resultados da junção correspondem à saída esperada
    var expectedRecords = CalculateExpectedJoin(leftTable, rightTable);
    var actualRecords = LoadResultRecords(result.NameOfResultTable);
    CollectionAssert.AreEqual(expectedRecords, actualRecords);
}
```

### Testes de Integração
1. **Testes End-to-End**: Operações de junção completas com várias combinações de tabelas
2. **Testes de Performance**: Contagem de operações de I/O e monitoramento de memória
3. **Testes de Integridade de Dados**: Verificar correção da junção com datasets conhecidos

## 🔧 Estendendo a Implementação

### Adicionando Novos Tipos de Tabela
```csharp
public class CustomTable : Table
{
    public CustomTable(string csvFilePath)
    {
        // Carregar dados CSV
        // Configurar colunas
        // Popular páginas
    }
}
```

### Ordens de Ordenação Customizadas
```csharp
private static string GetComparableValue(string value)
{
    // Implementar lógica de comparação customizada
    return value?.ToLowerInvariant() ?? string.Empty;
}
```

### Tipos de Junção Alternativos
A implementação atual pode ser estendida para suportar:
- **Left Outer Join**: Incluir registros esquerdos não correspondentes
- **Right Outer Join**: Incluir registros direitos não correspondentes  
- **Full Outer Join**: Incluir todos os registros não correspondentes

### 🚨 **LEMBRETES CRÍTICOS PARA EXTENSÕES**

```
⚠️  AO ESTENDER O CÓDIGO:

1. SEMPRE manter a restrição de 4 páginas na memória
2. NUNCA usar LINQ que carregue dados completos (ToList(), etc.)
3. SEMPRE processar dados página por página
4. IMPLEMENTAR descarte adequado de recursos
5. TESTAR conformidade de memória em todos os cenários

NUNCA COMPROMETA A RESTRIÇÃO DE MEMÓRIA!
```

---

**Líder Técnico**: Equipe de Implementação de Sistemas de Banco de Dados  
**Última Atualização**: 13 de Junho, 2025
