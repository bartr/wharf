package main

import "testing"

// TestGreetNonEmpty is a pass-to-pass guard: greet must never return an empty
// string. It passes both before and after the task is solved.
func TestGreetNonEmpty(t *testing.T) {
	if greet() == "" {
		t.Fatal("greet() returned an empty string")
	}
}
