using Fovium.Tools.ColorTaxonomyAudit;

return args.Length > 0 && args[0].Equals("report", StringComparison.OrdinalIgnoreCase)
    ? ColorSemanticsReportApplication.Run(args[1..], Console.Out, Console.Error)
    : ColorTaxonomyAuditApplication.Run(args, Console.Out, Console.Error);