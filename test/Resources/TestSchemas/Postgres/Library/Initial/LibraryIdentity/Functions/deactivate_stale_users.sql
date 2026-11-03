CREATE FUNCTION library_identity.deactivate_stale_users()
RETURNS void
LANGUAGE SQL
AS $$
    UPDATE library_identity.site_user
    SET is_active = FALSE
    WHERE is_active = TRUE
      AND last_login < CURRENT_TIMESTAMP - INTERVAL '1 year'
$$;
