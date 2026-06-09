import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateBatch34TargetedPromptQueues as validator  # noqa: E402


NEGATIVE = (
    "no readable text, no labels, no logo, no watermark-like decorative mark, "
    "no mobile-game icon composition, no baked directional light, no cropped atlas islands"
)


class Batch34TargetedPromptQueueTests(unittest.TestCase):
    def setUp(self) -> None:
        self.original_readme = validator.README
        self.original_static_preflight = validator.STATIC_PREFLIGHT
        self.original_fix_prompts = validator.FIX_PROMPTS
        self.original_regen_prompts = validator.REGEN_PROMPTS

    def tearDown(self) -> None:
        validator.README = self.original_readme
        validator.STATIC_PREFLIGHT = self.original_static_preflight
        validator.FIX_PROMPTS = self.original_fix_prompts
        validator.REGEN_PROMPTS = self.original_regen_prompts

    def configure_fixture(self, root: Path, include_supersession: bool = True) -> tuple[str, str]:
        validator.FIX_PROMPTS = root / "3405_TEXTURE_SOURCE_FIX_PROMPTS_20260608.md"
        validator.REGEN_PROMPTS = root / "3406_TEXTURE_SOURCE_REGEN_TARGETS_20260608.md"
        validator.README = root / "README.md"
        validator.STATIC_PREFLIGHT = root / "RunGeminiMaterialStaticPreflight.ps1"

        fix_jobs = "\n".join(
            f"## B34-FIX-{source_id} - Fix {source_id}\n\n```text\nPrompt {source_id}\n```"
            for source_id in validator.REQUIRED_FIX_IDS
        )
        validator.FIX_PROMPTS.write_text(
            "# Fix\n\nStatus: TARGETED_REGEN_QUEUE\n\n"
            "Global negative prompt when the service supports a separate negative field:\n\n"
            f"```text\n{NEGATIVE}\n```\n\n"
            + fix_jobs,
            encoding="utf-8",
        )

        superseded = ", ".join(f"`B34-FIX-{source_id}`" for source_id in validator.SUPERSEDED_FIX_IDS)
        supersession = (
            f"Supersedes selected older fix prompts in: `{validator.display(validator.FIX_PROMPTS)}`\n"
            f"Superseded older fix IDs: {superseded}\n\n"
            "For the four IDs above, use this file instead of the older `3405` fix prompts.\n\n"
            if include_supersession
            else ""
        )
        regen_jobs = "\n".join(
            f"## B34-{source_id}-R1 - Regen {source_id}\n\n"
            f"Type: SEAMLESS_TILE\nUse: test\nReason: test\n\nPrompt:\n\n```text\nPrompt {source_id}\n```"
            for source_id in validator.REQUIRED_REGEN_IDS + validator.OPTIONAL_REGEN_IDS
        )
        validator.REGEN_PROMPTS.write_text(
            "# Regen\n\nStatus: TARGETED_DIRECT_SERVICE_SUBMISSION_QUEUE\n"
            + supersession
            + "Required now:\n"
            + "\n".join(f"- `B34-{source_id}`" for source_id in validator.REQUIRED_REGEN_IDS)
            + "\n\nOptional backup:\n"
            + "\n".join(f"- `B34-{source_id}`" for source_id in validator.OPTIONAL_REGEN_IDS)
            + "\n\nGlobal negative prompt when the service supports a separate negative field:\n\n"
            f"```text\n{NEGATIVE}\n```\n\n"
            + regen_jobs,
            encoding="utf-8",
        )

        fix_display = validator.display(validator.FIX_PROMPTS)
        regen_display = validator.display(validator.REGEN_PROMPTS)
        validator_display = validator.display(validator.TARGETED_PROMPT_QUEUE_VALIDATOR)
        validator.README.write_text(f"{fix_display}\n{regen_display}\n{validator_display}\n", encoding="utf-8")
        validator.STATIC_PREFLIGHT.write_text("ValidateBatch34TargetedPromptQueues.py\n", encoding="utf-8")
        return fix_display, regen_display

    def run_validation(self) -> list[str]:
        errors: list[str] = []
        fix_text = validator.read(validator.FIX_PROMPTS, errors)
        regen_text = validator.read(validator.REGEN_PROMPTS, errors)
        validator.validate_negative(validator.FIX_PROMPTS, fix_text, errors)
        validator.validate_negative(validator.REGEN_PROMPTS, regen_text, errors)
        validator.validate_fix_prompts(fix_text, errors)
        validator.validate_regen_prompts(regen_text, errors)
        validator.validate_cross_links(errors)
        return errors

    def test_accepts_complete_targeted_prompt_queues(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            self.configure_fixture(Path(temp))

            self.assertEqual([], self.run_validation())

    def test_rejects_missing_supersession_for_overlapping_regen_ids(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            self.configure_fixture(Path(temp), include_supersession=False)

            errors = self.run_validation()

            self.assertTrue(any("missing superseded fix token" in error for error in errors), errors)
            self.assertTrue(any("missing operator supersession instruction" in error for error in errors), errors)

    def test_rejects_readme_without_targeted_validator_link(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fix_display, regen_display = self.configure_fixture(Path(temp))
            validator.README.write_text(f"{fix_display}\n{regen_display}\n", encoding="utf-8")

            errors = self.run_validation()

            self.assertTrue(any("ValidateBatch34TargetedPromptQueues.py" in error for error in errors), errors)


if __name__ == "__main__":
    unittest.main()
