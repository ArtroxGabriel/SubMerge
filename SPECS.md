# TRABALHO II - SORT-MERGE

## 1. Aspectos Gerais

Este trabalho visa a implementação da operação **Junção Sort/Merge (Sort Merge Join)** da álgebra relacional, utilizando a versão **External Merge Sort** para a ordenação externa das tabelas. As tabelas ordenadas devem ser gravadas em arquivos no disco antes da etapa de comparação. A implementação deve desconsiderar o uso de SGBDs, utilizando apenas o sistema de arquivos do sistema operacional.

Os operadores implementados deverão processar duas tabelas de entrada conforme o esquema Entidade-Relacionamento apresentado na Figura 1 do documento original.

### 1.1. Esquema E-R (Resumo)

- **Tabela Vinho**:

    - Chave Primária (PK): `vinho_id`

    - Chaves Estrangeiras (FK):

        - `uva_id` (referencia `uva_id` da tabela Uva)

        - `pais_producao_id` (referencia `pais_id` da tabela País)

- **Tabela Uva**:

    - Chave Primária (PK): `uva_id`

    - Chave Estrangeira (FK): `pais_origem_id` (referencia `pais_id` da tabela País)

- **Tabela País**:

    - Chave Primária (PK): `pais_id`

## 2. Implementação

### 2.1. Linguagem de Programação

A implementação deverá ser realizada em **C, C++ ou C#**.

### 2.2. Saída do Operador

O resultado da operação de junção Sort/Merge deve conter as tuplas resultantes e ser salvo em disco. Além disso, a saída deve incluir os seguintes dados estatísticos:

1. Quantidade de IO's (vezes que uma página foi lida).

2. Quantidade de páginas gravadas em disco pela operação.

3. Quantidade de tuplas geradas na junção.

**Importante**: As tabelas originais não devem ser modificadas.

### 2.3. Mapeamento de Dados em Disco

Um banco de dados será mapeado em disco da seguinte forma:

- Cada tabela terá uma quantidade não definida de páginas.

- Cada página armazenará, no máximo, 10 tuplas.

- A estrutura da tupla é definida no esquema da Figura 1 do documento original.

### 2.4. Estruturas de Classes Propostas

A implementação das abstrações de `Tabela`, `Pagina` e `Tupla` deverá seguir as classes propostas na Figura 2 do documento original:

- **Tabela**:

    - `pags: List<Pagina>`

    - `qtd_pags: int`

    - `qtd_cols: int`

- **Pagina**:

    - `tuplas: Tupla[10]`

    - `qtd_tuplas_ocup: int`

- **Tupla**:

    - `cols: String[qtd_cols]`

Todas essas estruturas devem ser armazenadas em **arquivos de texto** no disco. A forma de mapeamento dessas estruturas em arquivos deve ser definida na implementação.

### 2.5. Ordenação Externa

Para a ordenação externa, será necessário manter **até 4 páginas** em memória. O arquivo `Main`, que será fornecido, deverá ser utilizado na implementação e contém as operações a serem implementadas, juntamente com casos de teste para validação.

A implementação deve ser genérica, preparada para receber diferentes tabelas na junção em qualquer ordem. Exemplos de junções que podem ser solicitadas:

- `Uva ⋈ uva_id=uva_id Vinho;`

- `Vinho ⋈ uva_id=uva_id Uva;`

- `Uva ⋈ pais_origem_id=pais_id Pais;`

## 3. Entrega

- **Data da Entrega**: Sexta-Feira, 13 de junho de 2025, até as 10h00.

- **Local**: Apresentação e arguição no LEC/DC, no horário da aula.

- **Envio**: O código do programa e os arquivos de resultado devem ser enviados pelo Classroom até o final do horário da entrega. Envios posteriores serão penalizados.

- **Dúvidas**: Podem ser enviadas aos monitores Gabriel Magalhães (gabriel.alves@lsbd.ufc.br) ou Antonio Alves (antonio.marreiras@lsbd.ufc.br).
