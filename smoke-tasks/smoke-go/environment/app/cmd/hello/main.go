// Command hello prints a greeting to stdout. It is the free-standing CLI used
// by the DeepSWE smoke-go benchmark task.
package main

import "fmt"

// greet returns the greeting printed by the CLI.
func greet() string {
	return "Hello"
}

func main() {
	fmt.Println(greet())
}
