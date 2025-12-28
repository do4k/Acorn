# Acorn API

## Overview
This API exposes character details from the Acorn database.

## Endpoints

### Get Character by ID
- **GET** `/api/characters/{id}`
- **Description:** Returns details for a character by their ID (primary key).
- **Response:** 200 OK with character details, or 404 if not found.

## Usage
- The API is available at `http://localhost:5005` by default.
- Swagger UI is enabled in development mode for interactive exploration.

## Future Improvements
- Add authentication/authorization.
- Support for other database engines.
- More endpoints for character creation, update, and deletion.

