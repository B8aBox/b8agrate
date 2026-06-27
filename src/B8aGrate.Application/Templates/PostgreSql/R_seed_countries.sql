INSERT INTO public.country (code, name)
VALUES ('US', 'United States'),
       ('CA', 'Canada'),
       ('MX', 'Mexico') ON CONFLICT (code) DO NOTHING;
