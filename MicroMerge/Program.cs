using MicroMerge.Operator;
using MicroMerge.Tables;

void PrintResult(OperatorResult result)
{
    Console.WriteLine("Operação concluída com sucesso!");
    Console.WriteLine("Result:");
    Console.WriteLine("\tNúmero de páginas geradas: " + result.NumberOfCreatedPages);
    Console.WriteLine("\tNúmero de registros gerados: " + result.NumberOfCreatedRecords);
    Console.WriteLine("\tNúmero de IO's: " + result.NumberOfIOOperations);
    Console.WriteLine("\tNome da tabela gerada: " + result.NameOfResultTable);
}

var wineTable = new WineTable("./Data/vinho.csv");
var countryTable = new CountryTable("./Data/pais.csv");
var grapeTable = new GrapeTable("./Data/uva.csv");

// using var op = new Operator(wineTable, countryTable, "pais_producao_id", "pais_id");
// using var op = new Operator(countryTable, wineTable, "pais_id", "pais_producao_id" );
// using var op = new Operator(wineTable, grapeTable, "uva_id", "uva_id");
// using var op = new Operator(grapeTable, wineTable, "uva_id", "uva_id");
// using var op = new Operator(grapeTable, countryTable, "pais_origem_id", "pais_id");
using var op = new Operator(countryTable, grapeTable, "sigla", "pais_origem_id");


var res = op.Execute();
PrintResult(res);

op.WriteToCsv("./output");
