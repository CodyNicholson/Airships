#!/usr/bin/env python3
"""
Validates a game design specification JSON file against the Four-Books
curriculum requirements and flags known real-world case study anti-patterns.
"""

import json
import sys
from pathlib import Path

REQUIRED_FIELDS = {
    "title_and_hook": "Working title and 1-sentence high-concept hook (Rogers One-Sheet)",
    "target_platform": "Target platform: must be 'web', 'mobile', or 'desktop'",
    "player_experience_goals": "What the player should feel, think, and do (Fullerton Playcentric Goal)",
    "core_toy_and_3cs": "Goal-less toy appeal (Schell Lens #17) + Character, Camera, Controls (Rogers)",
    "formal_loop_and_economy": "Core loop, win/loss state, resource sources/drains, and feedback loops",
    "elemental_tetrad_and_theme": "Unifying theme across Aesthetics, Mechanics, Story, Technology (Schell)",
    "narrative_and_onboarding": "String of Pearls vs Story Machine + Training Cage onboarding",
    "monetization_and_scope": "Business model (Schell Filter #5) and MVP level/feature scope",
}

VALID_PLATFORMS = {"web", "mobile", "desktop"}


def audit_design_smells(spec: dict) -> list[str]:
    warnings = []
    blob = json.dumps(spec).lower()
    platform = str(spec.get("target_platform", "")).lower().strip()

    # Smell 1: Anthem scaling trap
    if "invisible scaling" in blob or "enemies scale to match" in blob:
        warnings.append(
            "[RISK: ANTHEM (2019)] Detected automatic enemy scaling. If enemies scale 1:1 with player gear, "
            "upgrades lose Discernibility and Integration (Salen & Zimmerman). Prefer horizontal capability unlocks."
        )

    # Smell 2: Diablo III RMAH / Endogenous value trap
    if any(k in blob for k in ["real money auction", "pay to win", "buy best gear", "cash for loot"]):
        warnings.append(
            "[RISK: DIABLO III (2012)] Detected real-money power trading. Allowing players to buy core rewards "
            "destroys Endogenous Value (Schell Lens #2). Keep monetization cosmetic or expansion-based."
        )

    # Smell 3: Balan Wonderworld / Mobile button bloat
    if "one button for everything" in blob or "single button" in blob:
        warnings.append(
            "[RISK: BALAN WONDERWORLD (2021)] Detected overly restricted single-button mapping. Never delete "
            "core traversal verbs (like jumping or dodging) when switching states or costumes (Rogers Three Cs)."
        )

    if platform == "mobile" and any(k in blob for k in ["10 buttons", "keyboard", "right click", "complex hotbar"]):
        warnings.append(
            "[RISK: PLATFORM MISMATCH (MOBILE)] Detected desktop-style multi-button complexity on a mobile target. "
            "Consolidate inputs into thumb-zone virtual sticks, contextual taps, or gesture controls."
        )

    # Smell 4: Missing Negative Feedback / Rubber-banding
    economy_text = str(spec.get("formal_loop_and_economy", "")).lower()
    if economy_text and not any(k in economy_text for k in ["drain", "sink", "negative feedback", "catch-up", "upkeep"]):
        warnings.append(
            "[RISK: RUNAWAY ECONOMY] 'formal_loop_and_economy' does not mention a resource drain/sink or "
            "negative feedback stabilizer (Fullerton / Salen & Zimmerman). Risk of runaway inflation or snowballing."
        )

    # Smell 5: Text-heavy tutorial
    onboarding_text = str(spec.get("narrative_and_onboarding", "")).lower()
    if any(k in onboarding_text for k in ["text popup", "manual", "wall of text", "slideshow tutorial"]):
        warnings.append(
            "[RISK: ONBOARDING FRICTION] Detected text-popup tutorials. Replace with Scott Rogers' 'Training Cage' "
            "— a safe, gated room where the player must physically perform the new mechanic once to advance."
        )

    return warnings


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: python3 validate_design_spec.py <path/to/spec.json>")
        return 1

    spec_path = Path(sys.argv[1])
    if not spec_path.exists():
        print(f"ERROR: Spec file not found: {spec_path}")
        return 1

    try:
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        print(f"ERROR: Invalid JSON in {spec_path}: {exc}")
        return 1

    missing = []
    empty_or_vague = []

    for field, desc in REQUIRED_FIELDS.items():
        if field not in spec or spec[field] is None:
            missing.append(f"- `{field}`: {desc}")
        elif isinstance(spec[field], str) and len(spec[field].strip()) < 10 and field != "target_platform":
            empty_or_vague.append(f"- `{field}` is too brief ('{spec[field]}'). Needs actionable detail: {desc}")

    platform = str(spec.get("target_platform", "")).lower().strip()
    if platform and platform not in VALID_PLATFORMS:
        empty_or_vague.append(
            f"- `target_platform` is '{spec.get('target_platform')}', but must be one of: {sorted(VALID_PLATFORMS)}"
        )

    warnings = audit_design_smells(spec)

    print("=== DESIGN-GAME SPEC VALIDATION REPORT ===")
    if missing or empty_or_vague:
        print("STATUS: INCOMPLETE (Clarification Required Before Generating Plan)\n")
        if missing:
            print("Missing Required Fields:")
            print("\n".join(missing))
            print()
        if empty_or_vague:
            print("Ambiguous / Under-specified Fields:")
            print("\n".join(empty_or_vague))
            print()
    else:
        print(f"STATUS: VALIDATED (Target Platform: {platform.upper()})\n")

    if warnings:
        print("Case-Study Diagnostic Warnings & Recommendations:")
        for w in warnings:
            print(f"  * {w}")
    else:
        print("Case-Study Diagnostic Warnings: None detected.")

    return 0 if not (missing or empty_or_vague) else 2


if __name__ == "__main__":
    sys.exit(main())

