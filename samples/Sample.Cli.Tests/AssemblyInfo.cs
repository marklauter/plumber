// TelemetrySummaryReflectsAPipelineRunAsync registers process-global OpenTelemetry listeners on the
// pipeline's ActivitySource and Meter. Running test classes in parallel would let other pipeline runs leak
// spans and metrics into its collections, so this assembly runs its tests serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
