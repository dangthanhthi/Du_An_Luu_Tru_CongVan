import importlib.util
from pathlib import Path
import unittest
from http.server import BaseHTTPRequestHandler, HTTPServer
import threading

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('configured_linux',ROOT/'scripts/qa/run-configured-linux.py')
qa=importlib.util.module_from_spec(spec);spec.loader.exec_module(qa)


class ConfiguredLinuxTests(unittest.TestCase):
    def test_windows_case_collisions_cannot_preserve_an_old_signing_value(self):
        env=qa.env_with({'JWT__SECRET':'old-synthetic','Other':'keep'},{'Jwt__Secret':'fresh-synthetic'})
        self.assertEqual([('Jwt__Secret','fresh-synthetic')],[(k,v) for k,v in env.items() if k.upper()=='JWT__SECRET'])
        self.assertEqual('keep',env['Other'])

    def test_previous_enabled_worker_values_are_removed_case_insensitively(self):
        builder=qa.module('build-core-images')
        env=qa.env_with({k.upper():'true' for k in builder.DISABLED},builder.DISABLED)
        self.assertEqual(len(builder.DISABLED),len(env))
        self.assertTrue(all(v=='false' for v in env.values()))

    def test_public_or_misleading_destinations_are_rejected_before_network(self):
        for address in ('http://example.invalid:80','http://127.0.0.1.evil.test:8080','https://127.0.0.1:8080'):
            with self.subTest(address=address),self.assertRaises(ValueError):qa.request(address,'/health')

    def test_redirect_is_returned_without_a_second_connection(self):
        class Redirect(BaseHTTPRequestHandler):
            def do_GET(self):
                self.send_response(302);self.send_header('Location','http://127.0.0.1:1/forbidden');self.end_headers()
            def log_message(self,*args):pass
        server=HTTPServer(('127.0.0.1',0),Redirect);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
        try:
            status,headers,_=qa.request('http://127.0.0.1:'+str(server.server_port),'/health')
            self.assertEqual(302,status)
        finally:server.shutdown();thread.join();server.server_close()

    def test_prepared_gateway_uses_only_private_core_aliases_and_excludes_deferred_routes(self):
        config=qa.configured_gateway();self.assertGreater(len(config['Routes']),10)
        for route in config['Routes']:
            self.assertFalse(route['UpstreamPathTemplate'].startswith(('/api/ai-ocr','/api/email-worker')))
            for endpoint in route['DownstreamHostAndPorts']:
                self.assertIn(endpoint['Host'],('auth','document','files','notification','partner'))
                self.assertEqual(8080,endpoint['Port'])
        self.assertTrue(any(route['UpstreamPathTemplate'].startswith('/api/notifications') for route in config['Routes']))


if __name__=='__main__':unittest.main()
