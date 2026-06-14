// The tracing and metrics middleware emit through a process-global ActivitySource and Meter, and the
// test collectors subscribe by name. Parallel test classes would cross-contaminate each other's spans and
// measurements, so this assembly runs its tests serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
