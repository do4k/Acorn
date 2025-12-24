UPDATE Accounts
SET Password = @Password,
    FullName = @FullName,
    Location = @Location,
    Email    = @Email,
    Country  = @Country,
    Created  = @Created,
    LastUsed = @LastUsed
WHERE Username = @Username