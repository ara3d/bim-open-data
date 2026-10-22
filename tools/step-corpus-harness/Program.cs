using Ara3D.Step.CorpusHarness;

var options = Options.Parse(args);
return options.Rest switch
{
    ["one", var file, var json] => OneMode.Run(file, json, options),
    ["all", var corpus, var outDir, var seconds] => AllMode.Run(corpus, outDir, int.Parse(seconds), options),
    ["time", var file, var prefix] => TimeMode.Run(file, prefix, options),
    ["laws", var file, var prefix] => LawsMode.Run(file, prefix, options),
    ["gallery", var source, var glb, .. var rest] => GalleryMode.Run(source, glb, rest, options),
    ["extract", var source, var faces, var plato, .. var rest] => ExtractMode.Run(source, faces, plato, rest, options),
    _ => Options.Usage(),
};
