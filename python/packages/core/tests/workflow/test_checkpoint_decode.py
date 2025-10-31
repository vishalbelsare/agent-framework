# Copyright (c) Microsoft. All rights reserved.

from dataclasses import dataclass  # noqa: I001
from typing import Any, cast

from agent_framework._workflows._checkpoint_encoding import (
    decode_checkpoint_value,
    encode_checkpoint_value,
)
from agent_framework._workflows._typing_utils import is_instance_of


@dataclass
class SampleRequest:
    """Sample request message for testing checkpoint encoding/decoding."""

    request_id: str
    prompt: str


@dataclass
class SampleResponse:
    """Sample response message for testing checkpoint encoding/decoding."""

    data: str
    original_request: SampleRequest
    request_id: str


def test_decode_dataclass_with_nested_request() -> None:
    """Test that dataclass with nested dataclass fields can be encoded and decoded correctly."""
    original = SampleResponse(
        data="approve",
        original_request=SampleRequest(request_id="abc", prompt="prompt"),
        request_id="abc",
    )

    encoded = encode_checkpoint_value(original)
    decoded = cast(SampleResponse, decode_checkpoint_value(encoded))

    assert isinstance(decoded, SampleResponse)
    assert decoded.data == "approve"
    assert decoded.request_id == "abc"
    assert isinstance(decoded.original_request, SampleRequest)
    assert decoded.original_request.prompt == "prompt"
    assert decoded.original_request.request_id == "abc"


def test_is_instance_of_coerces_nested_dataclass_dict() -> None:
    """Test that is_instance_of can handle nested structures with dict conversion."""
    response = SampleResponse(
        data="approve",
        original_request=SampleRequest(request_id="req-1", prompt="prompt"),
        request_id="req-1",
    )

    # Simulate checkpoint decode fallback leaving a dict
    response.original_request = cast(
        Any,
        {
            "request_id": "req-1",
            "prompt": "prompt",
        },
    )

    assert is_instance_of(response, SampleResponse)
    assert isinstance(response.original_request, dict)

    # Verify the dict contains expected values
    dict_request = cast(dict[str, Any], response.original_request)
    assert dict_request["request_id"] == "req-1"
    assert dict_request["prompt"] == "prompt"


def test_encode_decode_simple_dataclass() -> None:
    """Test encoding and decoding of a simple dataclass."""
    original = SampleRequest(request_id="test-123", prompt="test prompt")

    encoded = encode_checkpoint_value(original)
    decoded = cast(SampleRequest, decode_checkpoint_value(encoded))

    assert isinstance(decoded, SampleRequest)
    assert decoded.request_id == "test-123"
    assert decoded.prompt == "test prompt"


def test_encode_decode_nested_structures() -> None:
    """Test encoding and decoding of complex nested structures."""
    nested_data = {
        "requests": [
            SampleRequest(request_id="req-1", prompt="first prompt"),
            SampleRequest(request_id="req-2", prompt="second prompt"),
        ],
        "responses": {
            "req-1": SampleResponse(
                data="first response",
                original_request=SampleRequest(request_id="req-1", prompt="first prompt"),
                request_id="req-1",
            ),
        },
    }

    encoded = encode_checkpoint_value(nested_data)
    decoded = decode_checkpoint_value(encoded)

    assert isinstance(decoded, dict)
    assert "requests" in decoded
    assert "responses" in decoded

    # Check the requests list
    requests = cast(list[Any], decoded["requests"])
    assert isinstance(requests, list)
    assert len(requests) == 2
    assert all(isinstance(req, SampleRequest) for req in requests)
    first_request = cast(SampleRequest, requests[0])
    second_request = cast(SampleRequest, requests[1])
    assert first_request.request_id == "req-1"
    assert second_request.request_id == "req-2"

    # Check the responses dict
    responses = cast(dict[str, Any], decoded["responses"])
    assert isinstance(responses, dict)
    assert "req-1" in responses
    response = cast(SampleResponse, responses["req-1"])
    assert isinstance(response, SampleResponse)
    assert response.data == "first response"
    assert isinstance(response.original_request, SampleRequest)
    assert response.original_request.request_id == "req-1"
